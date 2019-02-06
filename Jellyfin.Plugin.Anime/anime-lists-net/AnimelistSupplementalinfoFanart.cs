using System.Xml.Serialization;

namespace AnimeLists
{
    /// <remarks />
    [XmlType(AnonymousType = true)]
    public class AnimelistSupplementalinfoFanart
    {
        /// <remarks />
        [XmlElement("thumb")]
        public AnimelistSupplementalinfoFanartThumb Thumb { get; set; }
    }
}