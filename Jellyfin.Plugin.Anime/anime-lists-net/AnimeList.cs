using System.Xml.Serialization;

namespace AnimeLists
{
    [XmlType(AnonymousType = true)]
    [XmlRoot("anime-list", Namespace = "", IsNullable = false)]
    public class Animelist
    {
        [XmlElement("anime")]
        public AnimelistAnime[] Anime { get; set; }
    }
}
