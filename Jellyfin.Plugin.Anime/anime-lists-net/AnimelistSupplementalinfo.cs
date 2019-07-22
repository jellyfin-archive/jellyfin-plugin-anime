using System.Xml.Serialization;

namespace AnimeLists
{
    [XmlType(AnonymousType = true)]
    public class AnimelistSupplementalinfo
    {
        [XmlElement("credits", typeof(string))]
        [XmlElement("director", typeof(string))]
        [XmlElement("fanart", typeof(AnimelistSupplementalinfoFanart))]
        [XmlElement("genre", typeof(string))]
        [XmlElement("studio", typeof(string))]
        [XmlChoiceIdentifier("ItemsElementName")]
        public object[] Items { get; set; }

        [XmlElement("ItemsElementName")]
        [XmlIgnore]
        public ItemsChoiceType[] ItemsElementName { get; set; }

        [XmlAttribute("replace")]
        public bool Replace { get; set; }

        [XmlIgnore]
        public bool ReplaceSpecified { get; set; }
    }
}
