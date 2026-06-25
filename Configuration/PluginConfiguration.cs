using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ScreenTime.Configuration;

/// <summary>
/// Daily limit for a single user.
/// </summary>
public class UserLimit
{
    /// <summary>Gets or sets the Jellyfin user id (GUID as string).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name (dashboard only, not authoritative).</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets the daily limit in minutes. 0 = no limit.</summary>
    public int DailyLimitMinutes { get; set; }
}

/// <summary>
/// Plugin configuration for ScreenTime.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets a value indicating whether tracking and enforcement are active.</summary>
    public bool EnableTracking { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the currently playing item is also stopped once the
    /// limit is exceeded. Default false = fair soft block (the running episode may finish, only new
    /// playbacks are blocked).
    /// </summary>
    public bool HardStop { get; set; }

    /// <summary>Gets or sets the hour (0-23) at which the daily count resets. Default 0 = midnight.</summary>
    public int ResetHour { get; set; }

    /// <summary>Gets or sets the header of the message shown to the user when blocked.</summary>
    public string BlockMessageHeader { get; set; } = "Screen time is up";

    /// <summary>Gets or sets the text of the message shown to the user when blocked.</summary>
    public string BlockMessageText { get; set; } = "That's it for today - see you tomorrow!";

    /// <summary>Gets or sets the per-user daily limits.</summary>
    public UserLimit[] UserLimits { get; set; } = System.Array.Empty<UserLimit>();
}
