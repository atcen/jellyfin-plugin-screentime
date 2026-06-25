using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ScreenTime;

/// <summary>
/// Persistent store for watched minutes per user and counting day.
/// Key: "{userId}|{yyyy-MM-dd}" → minutes.
/// </summary>
public class WatchTimeStore
{
    private readonly ILogger<WatchTimeStore> _logger;
    private readonly string _filePath;
    private readonly ConcurrentDictionary<string, int> _minutes;
    private readonly object _saveLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="WatchTimeStore"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public WatchTimeStore(ILogger<WatchTimeStore> logger)
    {
        _logger = logger;
        var dataFolder = Plugin.Instance?.DataFolderPath
            ?? Path.Combine(Path.GetTempPath(), "screentime");
        Directory.CreateDirectory(dataFolder);
        _filePath = Path.Combine(dataFolder, "watchtime.json");
        _minutes = Load();
    }

    /// <summary>
    /// Computes the counting day for a point in time, honouring the configured reset hour.
    /// </summary>
    /// <param name="now">Current local time.</param>
    /// <param name="resetHour">Hour of the daily reset (0-23).</param>
    /// <returns>Date string (yyyy-MM-dd) of the counting day.</returns>
    public static string TrackingDate(DateTime now, int resetHour)
        => now.AddHours(-Math.Clamp(resetHour, 0, 23)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Adds one minute for the user on the given counting day.</summary>
    /// <param name="userId">User id.</param>
    /// <param name="trackingDate">Counting day.</param>
    /// <returns>New minute total.</returns>
    public int AddMinute(string userId, string trackingDate)
    {
        var key = userId + "|" + trackingDate;
        var value = _minutes.AddOrUpdate(key, 1, (_, current) => current + 1);
        Save();
        return value;
    }

    /// <summary>Returns the minutes counted for the user on the given counting day.</summary>
    /// <param name="userId">User id.</param>
    /// <param name="trackingDate">Counting day.</param>
    /// <returns>Minutes.</returns>
    public int GetMinutes(string userId, string trackingDate)
        => _minutes.TryGetValue(userId + "|" + trackingDate, out var v) ? v : 0;

    /// <summary>Removes entries older than the given number of days.</summary>
    /// <param name="keepDays">How many days to keep.</param>
    public void Prune(int keepDays)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-keepDays);
        foreach (var key in _minutes.Keys.ToList())
        {
            var parts = key.Split('|');
            if (parts.Length == 2
                && DateTime.TryParseExact(parts[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                && d < cutoff)
            {
                _minutes.TryRemove(key, out _);
            }
        }

        Save();
    }

    private ConcurrentDictionary<string, int> Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                if (dict is not null)
                {
                    return new ConcurrentDictionary<string, int>(dict);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ScreenTime: could not load watchtime.json, starting empty");
        }

        return new ConcurrentDictionary<string, int>();
    }

    private void Save()
    {
        lock (_saveLock)
        {
            try
            {
                var json = JsonSerializer.Serialize(new Dictionary<string, int>(_minutes));
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ScreenTime: could not save watchtime.json");
            }
        }
    }
}
