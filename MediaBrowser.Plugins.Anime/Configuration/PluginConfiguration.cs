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
        public bool AutoCorrectSeriesPosters { get; set; }
        public List<string> IgnoredVirtualFolders { get; set; }
        public List<string> IgnoredPhysicalLocations { get; set; } 

        public static Func<PluginConfiguration> Instance { get; set; }

        public PluginConfiguration()
        {
            TitlePreference = TitlePreferenceType.Localized;
            AllowAutomaticMetadataUpdates = false;
            AutoCorrectSeriesPosters = true;
            IgnoredVirtualFolders = new List<string>();
            IgnoredPhysicalLocations = new List<string>();
        }
    }
}
