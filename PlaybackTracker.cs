using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ScreenTime.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ScreenTime;

/// <summary>
/// Background service that counts watched minutes per user and enforces the daily limit
/// (soft block on new playback; optional hard stop of the running item).
/// </summary>
public class PlaybackTracker : IHostedService, IDisposable
{
    private readonly ISessionManager _sessionManager;
    private readonly WatchTimeStore _store;
    private readonly ILogger<PlaybackTracker> _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackTracker"/> class.
    /// </summary>
    /// <param name="sessionManager">Jellyfin session manager.</param>
    /// <param name="store">Watch-time store.</param>
    /// <param name="logger">Logger.</param>
    public PlaybackTracker(ISessionManager sessionManager, WatchTimeStore store, ILogger<PlaybackTracker> logger)
    {
        _sessionManager = sessionManager;
        _store = store;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart += OnPlaybackStart;
        _loop = Task.Run(() => RunLoopAsync(_cts.Token), CancellationToken.None);
        _logger.LogInformation("ScreenTime: tracker started");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart -= OnPlaybackStart;
        _cts.Cancel();
        return Task.CompletedTask;
    }

    private static PluginConfiguration? Config => Plugin.Instance?.Configuration;

    private static UserLimit? FindLimit(PluginConfiguration config, Guid userId)
    {
        var id = userId.ToString("N");
        return config.UserLimits.FirstOrDefault(
            u => Guid.TryParse(u.UserId, out var g) && g.ToString("N") == id && u.DailyLimitMinutes > 0);
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        var lastPruneDay = -1;
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    Tick();
                    if (DateTime.Now.Day != lastPruneDay)
                    {
                        _store.Prune(14);
                        lastPruneDay = DateTime.Now.Day;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ScreenTime: error in tracking tick");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
    }

    private void Tick()
    {
        var config = Config;
        if (config is null || !config.EnableTracking)
        {
            return;
        }

        var date = WatchTimeStore.TrackingDate(DateTime.Now, config.ResetHour);

        // Count once per user, even across multiple parallel sessions (= real screen time).
        var activeUsers = _sessionManager.Sessions
            .Where(s => s.NowPlayingItem is not null && !(s.PlayState?.IsPaused ?? false))
            .Select(s => s.UserId)
            .Where(uid => uid != Guid.Empty)
            .Distinct();

        foreach (var userId in activeUsers)
        {
            var limit = FindLimit(config, userId);
            if (limit is null)
            {
                continue;
            }

            var minutes = _store.AddMinute(userId.ToString("N"), date);

            if (minutes >= limit.DailyLimitMinutes && config.HardStop)
            {
                StopUserSessions(userId, config);
            }
        }
    }

    private void OnPlaybackStart(object? sender, PlaybackProgressEventArgs e)
    {
        var config = Config;
        if (config is null || !config.EnableTracking)
        {
            return;
        }

        var session = e.Session;
        if (session is null || session.UserId == Guid.Empty)
        {
            return;
        }

        var limit = FindLimit(config, session.UserId);
        if (limit is null)
        {
            return;
        }

        var date = WatchTimeStore.TrackingDate(DateTime.Now, config.ResetHour);
        if (_store.GetMinutes(session.UserId.ToString("N"), date) < limit.DailyLimitMinutes)
        {
            return;
        }

        // Limit reached: stop this NEW playback, leave already-running sessions untouched.
        _logger.LogInformation(
            "ScreenTime: blocking new playback for user {UserId} (limit {Limit} min reached)",
            session.UserId,
            limit.DailyLimitMinutes);
        BlockSession(session.Id, config);
    }

    private void BlockSession(string sessionId, PluginConfiguration config)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _sessionManager.SendMessageCommand(
                    null,
                    sessionId,
                    new MessageCommand
                    {
                        Header = config.BlockMessageHeader,
                        Text = config.BlockMessageText,
                        TimeoutMs = 8000,
                    },
                    CancellationToken.None).ConfigureAwait(false);

                await _sessionManager.SendPlaystateCommand(
                    null,
                    sessionId,
                    new PlaystateRequest { Command = PlaystateCommand.Stop },
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ScreenTime: could not stop session {SessionId}", sessionId);
            }
        });
    }

    private void StopUserSessions(Guid userId, PluginConfiguration config)
    {
        foreach (var s in _sessionManager.Sessions.Where(s => s.UserId == userId && s.NowPlayingItem is not null))
        {
            BlockSession(s.Id, config);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
