using System.Xml.Serialization;

namespace AnimeLists
{
    [XmlType(AnonymousType = true)]
    public class AnimelistSupplementalinfoFanart
    {
        [XmlElement("thumb")]
        public AnimelistSupplementalinfoFanartThumb Thumb { get; set; }
    }
}
