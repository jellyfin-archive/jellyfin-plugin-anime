using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Plugins.Anime.Configuration;
using MediaBrowser.Plugins.Anime.Providers.AniDB.Identity;
using System;
using System.Collections.Generic;

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

        private Guid _id = new Guid("1d0dddf7-1877-4473-8d7b-03f7dac1e559");

        public override Guid Id
        {
            get { return _id; }
        }
    }
}