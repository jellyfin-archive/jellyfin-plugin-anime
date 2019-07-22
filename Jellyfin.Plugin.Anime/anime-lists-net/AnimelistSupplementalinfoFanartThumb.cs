using System.Xml.Serialization;

namespace AnimeLists
{
    [XmlType(AnonymousType = true)]
    public class AnimelistSupplementalinfoFanartThumb
    {
        [XmlAttribute("dim")]
        public string Dim { get; set; }

        [XmlAttribute("colors")]
        public string Colors { get; set; }

        [XmlAttribute("preview")]
        public string Preview { get; set; }

        [XmlText]
        public string Value { get; set; }
    }
}
