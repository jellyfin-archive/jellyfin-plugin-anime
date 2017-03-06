using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Plugins.Anime.Configuration;
using MediaBrowser.Plugins.Anime.Providers.AniDB.Converter;
using MediaBrowser.Plugins.Anime.Providers.AniDB.Identity;

namespace MediaBrowser.Plugins.Anime
{
    public class Plugin
        : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger logger) : base(applicationPaths, xmlSerializer)
        {
            Instance = this;

            AniDbTitleMatcher.DefaultInstance = new AniDbTitleMatcher(logger, new AniDbTitleDownloader(logger, applicationPaths));
        }

        public override string Name
        {
            get { return "Anime"; }
        }

        public static Plugin Instance { get; private set; }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "anime",
                    EmbeddedResourcePath = "MediaBrowser.Plugins.Anime.Configuration.configPage.html"
                }
            };
        }
    }
}
