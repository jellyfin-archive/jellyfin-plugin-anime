using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Anime.Configuration
{
    public enum TitlePreferenceType
    {
        /// <summary>
        /// Use titles in the local metadata language.
        /// </summary>
        Localized,

        /// <summary>
        /// Use titles in Japanese.
        /// </summary>
        Japanese,

        /// <summary>
        /// Use titles in Japanese romaji.
        /// </summary>
        JapaneseRomaji
    }

    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            TitlePreference = TitlePreferenceType.Localized;
            TidyGenreList = true;
            MaxGenres = 5;
            AddAnimeGenre = true;
            UseAnidbOrderingWithSeasons = false;
            AniDB_wait_time = 2000;
        }

        public TitlePreferenceType TitlePreference { get; set; }
        public bool TidyGenreList { get; set; }
        public int MaxGenres { get; set; }
        public bool AddAnimeGenre { get; set; }
        public bool UseAnidbOrderingWithSeasons { get; set; }
        public int AniDB_wait_time { get; set; }
    }
}
