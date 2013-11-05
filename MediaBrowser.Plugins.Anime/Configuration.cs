using MediaBrowser.Plugins.Anime.Providers.AniDB;

namespace MediaBrowser.Plugins.Anime
{
    public class Configuration
    {
        public static readonly Configuration Instance = new Configuration();

        public TitlePreferenceType TitlePreference { get; set; }
        public bool AllowAutomaticMetadataUpdates { get; set; }

        public Configuration()
        {
            TitlePreference = TitlePreferenceType.Localized;
            AllowAutomaticMetadataUpdates = false;
        }
    }
}
