using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace MediaBrowser.Plugins.AniDB
{
    public class Plugin
        : BasePlugin<PluginConfiguration>
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger logger) : base(applicationPaths, xmlSerializer)
        {
            AniDbTitleMatcher.DefaultInstance = new AniDbTitleMatcher(applicationPaths, logger);
        }

        public override string Name
        {
            get { return "AniDB"; }
        }
    }
}
