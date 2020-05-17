using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Anime.Configuration;
using Jellyfin.Plugin.Anime.Providers.AniDB.Identity;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Anime
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public Plugin(
            IApplicationPaths applicationPaths,
            IXmlSerializer xmlSerializer,
            ILogger logger)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;

            AniDbTitleMatcher.DefaultInstance = new AniDbTitleMatcher(
                logger,
                new AniDbTitleDownloader(logger, applicationPaths));
        }

        /// <inheritdoc />
        public override string Name => "Anime";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("a4df60c5-6ab4-412a-8f79-2cab93fb2bc5");

        public static Plugin Instance { get; private set; }

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = string.Format("{0}.Configuration.configPage.html", GetType().Namespace)
                }
            };
        }
    }
}
