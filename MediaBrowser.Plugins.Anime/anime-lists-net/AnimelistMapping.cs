using System.Collections.Generic;
using System.Xml.Serialization;

namespace AnimeLists
{
    /// <remarks />
    [XmlType(AnonymousType = true)]
    public class AnimelistMapping
    {
        /// <remarks />
        [XmlAttribute("anidbseason")]
        public byte AnidbSeason { get; set; }

        /// <remarks />
        [XmlAttribute("tvdbseason")]
        public byte TvdbSeason { get; set; }

        /// <remarks />
        [XmlAttribute("start")]
        public short Start { get; set; }

        /// <remarks />
        [XmlIgnore]
        public bool StartSpecified { get; set; }

        /// <remarks />
        [XmlAttribute("end")]
        public short End { get; set; }

        /// <remarks />
        [XmlIgnore]
        public bool EndSpecified { get; set; }

        /// <remarks />
        [XmlAttribute("offset")]
        public short Offset { get; set; }

        /// <remarks />
        [XmlIgnore]
        public bool OffsetSpecified { get; set; }

        /// <remarks />
        [XmlText]
        public string Value { get; set; }

        [XmlIgnore] public List<EpisodeMapping> ParsedMappings { get; set; }
    }
}