using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Plugins.AniDB.Providers;

namespace MediaBrowser.Plugins.AniDB
{
    public class Plugin
        : BasePlugin<PluginConfiguration>
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger logger) : base(applicationPaths, xmlSerializer)
        {
            AniDbTitleMatcher.DefaultInstance = new AniDbTitleMatcher(logger, new AniDbTitleDownloader(logger, applicationPaths));
        }

        public override string Name
        {
            get { return "AniDB"; }
        }
    }
}
