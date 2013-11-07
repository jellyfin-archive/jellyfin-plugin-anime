using MediaBrowser.Model.Plugins;

namespace MediaBrowser.Plugins.Anime.Configuration
{
    public enum TitlePreferenceType
    {
        Localized,
        Japanese,
        JapaneseRomaji,
    }

    public class PluginConfiguration
        : BasePluginConfiguration
    {
        private static PluginConfiguration _instance = new PluginConfiguration();

        public TitlePreferenceType TitlePreference { get; set; }
        public bool AllowAutomaticMetadataUpdates { get; set; }

        public PluginConfiguration()
        {
            TitlePreference = TitlePreferenceType.JapaneseRomaji;
            AllowAutomaticMetadataUpdates = false;
        }

        public static PluginConfiguration Instance
        {
            get { return _instance; }
            set { _instance = value; }
        }
    }
}
