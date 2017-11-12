using System.Xml.Serialization;

namespace AnimeLists
{
    /// <remarks />
    [XmlType(AnonymousType = true)]
    public class AnimelistSupplementalinfo
    {
        /// <remarks />
        [XmlElement("credits", typeof(string))]
        [XmlElement("director", typeof(string))]
        [XmlElement("fanart", typeof(AnimelistSupplementalinfoFanart))]
        [XmlElement("genre", typeof(string))]
        [XmlElement("studio", typeof(string))]
        [XmlChoiceIdentifier("ItemsElementName")]
        public object[] Items { get; set; }

        /// <remarks />
        [XmlElement("ItemsElementName")]
        [XmlIgnore]
        public ItemsChoiceType[] ItemsElementName { get; set; }

        /// <remarks />
        [XmlAttribute("replace")]
        public bool Replace { get; set; }

        /// <remarks />
        [XmlIgnore]
        public bool ReplaceSpecified { get; set; }
    }
}