using System.Xml.Serialization;

namespace AnimeLists
{
    [XmlType(AnonymousType = true)]
    public class AnimelistAnime
    {
        public string Name { get; set; }

        [XmlArray("mapping-list")]
        [XmlArrayItem("mapping", typeof(AnimelistMapping), IsNullable = false)]
        public AnimelistMapping[] Mappinglist { get; set; }

        [XmlElement("before")]
        public string Before { get; set; }

        [XmlElement("supplemental-info")]
        public AnimelistSupplementalinfo[] Supplementalinfo { get; set; }

        [XmlAttribute("anidbid")]
        public string AnidbId { get; set; }

        [XmlAttribute("anisearch")]
        public string AniSearchId { get; set; }

        [XmlAttribute("proxer")]
        public string ProxerId { get; set; }

        [XmlAttribute("tvdbid")]
        public string TvdbId { get; set; }

        [XmlAttribute("defaulttvdbseason")]
        public string DefaultTvdbSeason { get; set; }

        [XmlAttribute("imdbid")]
        public string ImdbId { get; set; }

        [XmlAttribute("tmdbid")]
        public string TmdbId { get; set; }

        [XmlAttribute("episodeoffset")]
        public short EpisodeOffset { get; set; }

        [XmlIgnore]
        public bool EpisodeOffsetSpecified { get; set; }
    }
}
