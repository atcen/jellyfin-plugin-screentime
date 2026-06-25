using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.ScreenTime.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.ScreenTime;

/// <summary>
/// ScreenTime – screen-time control for Jellyfin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="xmlSerializer">Jellyfin XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>Gets the global plugin instance for access from services.</summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "ScreenTime";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("4d7408ff-07b0-49e5-b1a8-d484b93fcde6");

    /// <inheritdoc />
    public override string Description => "Screen-time control: daily limits per child with a fair soft block (the running episode finishes, new playbacks are blocked).";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = string.Format(
                CultureInfo.InvariantCulture,
                "{0}.Configuration.configPage.html",
                GetType().Namespace),
            EnableInMainMenu = true,
            DisplayName = "ScreenTime",
            MenuIcon = "timer",
        };
    }
}
