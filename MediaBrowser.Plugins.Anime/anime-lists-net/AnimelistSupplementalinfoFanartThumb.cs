using System.Xml.Serialization;

namespace AnimeLists
{
    /// <remarks />
    [XmlType(AnonymousType = true)]
    public class AnimelistSupplementalinfoFanartThumb
    {
        /// <remarks />
        [XmlAttribute("dim")]
        public string Dim { get; set; }

        /// <remarks />
        [XmlAttribute("colors")]
        public string Colors { get; set; }

        /// <remarks />
        [XmlAttribute("preview")]
        public string Preview { get; set; }

        /// <remarks />
        [XmlText]
        public string Value { get; set; }
    }
}