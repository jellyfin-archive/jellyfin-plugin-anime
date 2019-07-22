using System.Collections.Generic;
using System.Xml.Serialization;

namespace AnimeLists
{
    [XmlType(AnonymousType = true)]
    public class AnimelistMapping
    {
        [XmlAttribute("anidbseason")]
        public byte AnidbSeason { get; set; }

        [XmlAttribute("tvdbseason")]
        public byte TvdbSeason { get; set; }

        [XmlAttribute("start")]
        public short Start { get; set; }

        [XmlIgnore]
        public bool StartSpecified { get; set; }

        [XmlAttribute("end")]
        public short End { get; set; }

        [XmlIgnore]
        public bool EndSpecified { get; set; }

        [XmlAttribute("offset")]
        public short Offset { get; set; }

        [XmlIgnore]
        public bool OffsetSpecified { get; set; }

        [XmlText]
        public string Value { get; set; }

        [XmlIgnore]
        public List<EpisodeMapping> ParsedMappings { get; set; }
    }
}
