using System.IO;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;

namespace MediaBrowser.Plugins.Anime.Configuration
{
    public class AnimeConfigurationPage
        : IPluginConfigurationPage
    {
        public Stream GetHtmlStream()
        {
            return GetType().Assembly.GetManifestResourceStream("MediaBrowser.Plugins.Anime.Configuration.configPage.html");
        }

        public string Name {
            get { return "Anime"; }
        }

        public ConfigurationPageType ConfigurationPageType {
            get { return ConfigurationPageType.PluginConfiguration; }
        }

        IPlugin IPluginConfigurationPage.Plugin {
            get { return Plugin.Instance; }
        }
    }
}
