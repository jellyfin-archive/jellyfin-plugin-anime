using MediaBrowser.Plugins.AniDB.Providers.AniDB;

namespace MediaBrowser.Plugins.AniDB
{
    public class Configuration
    {
        public static readonly Configuration Instance = new Configuration();

        public TitlePreferenceType TitlePreference { get; set; }

        public Configuration()
        {
            TitlePreference = TitlePreferenceType.Localized;
        }
    }
}
