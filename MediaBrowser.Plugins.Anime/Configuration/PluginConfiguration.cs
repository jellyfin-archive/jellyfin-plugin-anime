using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace MediaBrowser.Plugins.Anime.Configuration
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
        JapaneseRomaji,
    }

    public class PluginConfiguration
        : BasePluginConfiguration
    {
        public TitlePreferenceType TitlePreference { get; set; }
        public bool AllowAutomaticMetadataUpdates { get; set; }
        public HashSet<string> IgnoredVirtualFolders { get; set; }
        public HashSet<string> IgnoredPhysicalLocations { get; set; }
        public bool TidyGenreList { get; set; }
        public int MaxGenres { get; set; }
        public bool MoveExcessGenresToTags { get; set; }

        public static Func<PluginConfiguration> Instance { get; set; }

        public PluginConfiguration()
        {
            TitlePreference = TitlePreferenceType.Localized;
            AllowAutomaticMetadataUpdates = true;
            IgnoredVirtualFolders = new HashSet<string>();
            IgnoredPhysicalLocations = new HashSet<string>();
            TidyGenreList = true;
            MaxGenres = 5;
            MoveExcessGenresToTags = true;
        }
    }
}
