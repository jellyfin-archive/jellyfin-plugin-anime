using System.Collections.Generic;
using System.Linq;

namespace MediaBrowser.Plugins.Anime.Providers
{
    public class GenreHelper
    {
        private static readonly Dictionary<string, string> GenreMappings = new Dictionary<string, string>
        {
            {"Action", "Action"},
            {"Advanture", "Adventure"},
            {"Comedy", "Comedy"},
            {"Dementia", "Psychological Thriller"},
            {"Demons", "Fantasy"},
            {"Drama", "Drama"},
            {"Ecchi", "Ecchi"},
            {"Fantasy", "Fantasy"},
            {"Harem", "Harem"},
            {"Hentai", "Adult"},
            {"Historical", "Period & Historical"},
            {"Horror", "Horror"},
            {"Josei", "Josei"},
            {"Kids", "Kids"},
            {"Magic", "Fantasy"},
            {"Martial Arts", "Martial Arts"},
            {"Mecha", "Mecha"},
            {"Music", "Music"},
            {"Mystery", "Mystery"},
            {"Parody", "Comedy"},
            {"Psychological", "Psychological Thriller"},
            {"Romance", "Romance"},
            {"Sci-Fi", "Sci-Fi"},
            {"Seinen", "Seinen"},
            {"Shoujo", "Shoujo"},
            {"Shounen", "Shounen"},
            {"Slice of Life", "Slice of Life"},
            {"Space", "Sci-Fi"},
            {"Sports", "Sport"},
            {"Supernatural", "Supernatural"},
            {"Thriller", "Thriller"},
            {"Vampire", "Supernatural"},
            {"Yaoi", "Adult"},
            {"Yuri", "Adult"}
        };

        private static readonly string[] GenresAsTags =
        {
            "Hentai",
            "Space",
            "Vampire",
            "Yaoi",
            "Yuri"
        };

        public static void TidyGenres(SeriesInfo series)
        {
            var genres = new HashSet<string>();
            var tags = new HashSet<string>(series.Tags);

            foreach (string genre in series.Genres)
            {
                string mapped;
                if (GenreMappings.TryGetValue(genre, out mapped))
                    genres.Add(mapped);
                else
                    tags.Add(genre);

                if (GenresAsTags.Contains(genre))
                    tags.Add(genre);
            }

            series.Genres = genres.ToList();
            series.Tags = tags.ToList();
        }
    }
}