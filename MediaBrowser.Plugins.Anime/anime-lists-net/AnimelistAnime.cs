using System.Xml.Serialization;

namespace AnimeLists
{
    /// <remarks />
    [XmlType(AnonymousType = true)]
    public class AnimelistAnime
    {
        /// <remarks />
        public string Name { get; set; }

        /// <remarks />
        [XmlArray("mapping-list")]
        [XmlArrayItem("mapping", typeof(AnimelistMapping), IsNullable = false)]
        public AnimelistMapping[] Mappinglist { get; set; }

        /// <remarks />
        [XmlElement("before")]
        public string Before { get; set; }

        /// <remarks />
        [XmlElement("supplemental-info")]
        public AnimelistSupplementalinfo[] Supplementalinfo { get; set; }

        /// <remarks />
        [XmlAttribute("anidbid")]
        public string AnidbId { get; set; }

        /// <remarks />
        [XmlAttribute("anisearch")]
        public string AniSearchId { get; set; }

        /// <remarks />
        [XmlAttribute("proxer")]
        public string ProxerId { get; set; }

        /// <remarks />
        [XmlAttribute("myanimelist")]
        public string MyAnimeListId { get; set; }

        /// <remarks />
        [XmlAttribute("tvdbid")]
        public string TvdbId { get; set; }

        /// <remarks />
        [XmlAttribute("defaulttvdbseason")]
        public string DefaultTvdbSeason { get; set; }

        /// <remarks />
        [XmlAttribute("imdbid")]
        public string ImdbId { get; set; }

        /// <remarks />
        [XmlAttribute("tmdbid")]
        public string TmdbId { get; set; }

        /// <remarks />
        [XmlAttribute("episodeoffset")]
        public short EpisodeOffset { get; set; }

        /// <remarks />
        [XmlIgnore]
        public bool EpisodeOffsetSpecified { get; set; }
    }
}