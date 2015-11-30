using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Plugins.Anime.Configuration;

namespace MediaBrowser.Plugins.Anime.Providers
{
    public static class GenreHelper
    {
        private static readonly Dictionary<string, string> GenreMappings = new Dictionary<string, string>
        {
            {"Action", "Action"},
            {"Advanture", "Adventure"},
            {"Contemporary Fantasy", "Fantasy"},
            {"Comedy", "Comedy"},
            {"Dark Fantasy", "Fantasy"},
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
            {"Mahou Shoujo", "Mahou Shoujo"},
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
            {"Tragedy", "Tragedy"},
            {"Witch", "Supernatural"},
            {"Vampire", "Supernatural"},
            {"Yaoi", "Adult"},
            {"Yuri", "Adult"},
            {"Zombie", "Supernatural"}
        };

        private static readonly string[] GenresAsTags =
        {
            "Hentai",
            "Space",
            "Vampire",
            "Yaoi",
            "Yuri",
            "Zombie",
            "Demons",
            "Witch"
        };

        private static readonly Dictionary<string, string> IgnoreIfPresent = new Dictionary<string, string>
        {
            {"Psychological Thriller", "Thriller"}
        };

        public static void CleanupGenres(Series series)
        {
            PluginConfiguration config = PluginConfiguration.Instance();

            if (config.TidyGenreList)
            {
                series.Genres = RemoveRedundantGenres(series.Genres)
                                           .Where(g => !"Animation".Equals(g) && !"Anime".Equals(g))
                                           .Distinct()
                                           .ToList();

                TidyGenres(series);
            }

            if (config.MaxGenres > 0)
            {
                if (config.MoveExcessGenresToTags)
                {
                    foreach (string genre in series.Genres.Skip(config.MaxGenres - 1))
                    {
                        if (!series.Tags.Contains(genre))
                            series.Tags.Add(genre);
                    }
                }

                series.Genres = series.Genres.Take(config.MaxGenres - 1).ToList();
            }

            if (!series.Genres.Contains("Anime"))
                series.AddGenre("Anime");
        }

        public static void RemoveDuplicateTags(Series series)
        {
            for (int i = series.Tags.Count - 1; i >= 0; i--)
            {
                if (series.Genres.Contains(series.Tags[i]))
                    series.Tags.RemoveAt(i);
            }
        }

        public static void TidyGenres(Series series)
        {
            var config = PluginConfiguration.Instance != null ? PluginConfiguration.Instance() : new PluginConfiguration();

            var genres = new HashSet<string>();
            var tags = new HashSet<string>(series.Tags);

            foreach (string genre in series.Genres)
            {
                string mapped;
                if (GenreMappings.TryGetValue(genre, out mapped))
                    genres.Add(mapped);
                else
                {
                    if (config.MoveExcessGenresToTags)
                        tags.Add(genre);
                    else
                        genres.Add(genre);
                }

                if (GenresAsTags.Contains(genre))
                {
                    if (config.MoveExcessGenresToTags)
                        tags.Add(genre);
                    else if (!genres.Contains(genre))
                        genres.Add(genre);
                }
            }

            series.Genres = genres.ToList();
            series.Tags = tags.ToList();
        }

        public static IEnumerable<string> RemoveRedundantGenres(IEnumerable<string> genres)
        {
            var list = genres as IList<string> ?? genres.ToList();

            var toRemove = list.Where(IgnoreIfPresent.ContainsKey).Select(genre => IgnoreIfPresent[genre]).ToList();
            return list.Where(genre => !toRemove.Contains(genre));
        }
    }
}