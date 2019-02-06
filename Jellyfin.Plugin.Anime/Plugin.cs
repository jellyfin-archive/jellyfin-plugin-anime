using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Jellyfin.Plugin.Anime.Configuration;
using Jellyfin.Plugin.Anime.Providers.AniDB.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.Anime
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
                    EmbeddedResourcePath = "Jellyfin.Plugin.Anime.Configuration.configPage.html"
                }
            };
        }

        private Guid _id = new Guid("1d0dddf7-1877-4473-8d7b-03f7dac1e559");

        public override Guid Id
        {
            get { return _id; }
        }
    }
}
