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
            MaxGenres = 5;
            TidyGenreList = true;
            AddAnimeGenre = true;
            AniDbRateLimit = 2000;
            AniDbOrderWithSeasons = false;
            AniDbReplaceGraves = true;
        }

        public TitlePreferenceType TitlePreference { get; set; }

        public int MaxGenres { get; set; }

        public bool TidyGenreList { get; set; }

        public bool AddAnimeGenre { get; set; }

        public int AniDbRateLimit { get; set; }

        public bool AniDbOrderWithSeasons { get; set; }

        public bool AniDbReplaceGraves { get; set; }
    }
}
