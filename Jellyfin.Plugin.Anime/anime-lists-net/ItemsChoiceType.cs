using System.Xml.Serialization;

namespace AnimeLists
{
    /// <remarks />
    [XmlType(IncludeInSchema = false)]
    public enum ItemsChoiceType
    {
        credits,

        director,

        fanart,

        genre,

        studio
    }
}
