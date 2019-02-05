using System.Collections.Generic;
using System.Linq;

namespace AnimeLists
{
    public class Mapper
    {
        private readonly Dictionary<string, AnimelistAnime> _anidbMappings;
        private readonly Dictionary<string, List<AnimelistAnime>> _tvdbMappings;

        public Mapper(string animeListFile = "anime-list.xml")
            : this(new Downloader(animeListFile).Download().Result)
        {
        }

        public Mapper(Animelist list)
        {
            _anidbMappings = new Dictionary<string, AnimelistAnime>();
            _tvdbMappings = new Dictionary<string, List<AnimelistAnime>>();

            int n;
            foreach (var anime in list.Anime.Where(x => int.TryParse(x.TvdbId, out n)))
            {
                _anidbMappings[anime.AnidbId] = anime;

                List<AnimelistAnime> l;
                if (!_tvdbMappings.TryGetValue(anime.TvdbId, out l))
                {
                    l = new List<AnimelistAnime>();
                    _tvdbMappings[anime.TvdbId] = l;
                }

                l.Add(anime);
            }
        }

        public AnidbEpisode ToAnidb(TvdbEpisode tvdb)
        {
            List<AnimelistAnime> animeList;
            if (!_tvdbMappings.TryGetValue(tvdb.Series, out animeList))
                return null;

            // look for exact mapping in mapping list
            foreach (var anime in animeList.Where(x => x.Mappinglist != null))
            {
                var mappings = anime.Mappinglist.Where(x => x.TvdbSeason == tvdb.Season);
                foreach (var mapping in mappings)
                {
                    var episode = FindTvdbEpisodeMapping(tvdb, mapping);

                    if (episode != null)
                    {
                        return new AnidbEpisode
                        {
                            Series = anime.AnidbId,
                            Season = mapping.AnidbSeason,
                            Index = episode.Value
                        };
                    }
                }
            }

            var seasonMatch = animeList
                .Select(x => new { Season = Parse(x.DefaultTvdbSeason), Match = x })
                .Where(x => x.Season == tvdb.Season)
                .Select(x => new { Offset = x.Match.EpisodeOffsetSpecified ? x.Match.EpisodeOffset : 0, x.Match })
                .Where(x => x.Offset <= tvdb.Index)
                .OrderByDescending(x => x.Offset)
                .FirstOrDefault();

            if (seasonMatch != null)
            {
                return new AnidbEpisode
                {
                    Series = seasonMatch.Match.AnidbId,
                    Season = 1,
                    Index = tvdb.Index - seasonMatch.Offset
                };
            }

            // absolute episode numbers match
            var absolute = animeList.FirstOrDefault(x => x.DefaultTvdbSeason == "a");
            if (absolute != null)
            {
                return new AnidbEpisode
                {
                    Series = absolute.AnidbId,
                    Season = 1,
                    Index = tvdb.Index
                };
            }

            return null;
        }

        private int? Parse(string s)
        {
            int x;
            if (int.TryParse(s, out x))
                return x;

            return null;
        }

        public TvdbEpisode ToTvdb(AnidbEpisode anidb)
        {
            AnimelistAnime anime;
            if (!_anidbMappings.TryGetValue(anidb.Series, out anime))
                return null;

            // look for exact mapping in mapping list
            if (anime.Mappinglist != null)
            {
                var mappings = anime.Mappinglist.Where(x => x.AnidbSeason == anidb.Season);
                foreach (var mapping in mappings)
                {
                    var episode = FindAnidbEpisodeMapping(anidb, mapping);

                    if (episode != null)
                    {
                        return new TvdbEpisode
                        {
                            Series = anime.TvdbId,
                            Season = mapping.TvdbSeason,
                            Index = episode.Value
                        };
                    }
                }
            }

            // absolute episode numbers match
            var season = anime.DefaultTvdbSeason;
            if (season == "a")
            {
                return new TvdbEpisode
                {
                    Series = anime.TvdbId,
                    Season = null,
                    Index = anidb.Index
                };
            }

            // fallback to offset
            var offset = anime.EpisodeOffsetSpecified ? anime.EpisodeOffset : 0;

            return new TvdbEpisode
            {
                Series = anime.TvdbId,
                Season = int.Parse(season),
                Index = anidb.Index + offset
            };
        }

        private int? FindTvdbEpisodeMapping(TvdbEpisode tvdb, AnimelistMapping mapping)
        {
            var maps = GetEpisodeMappings(mapping);
            var exact = maps.FirstOrDefault(x => x.Tvdb == tvdb.Index);

            if (exact != null)
                return exact.Anidb;

            if (mapping.OffsetSpecified)
            {
                var startInRange = !mapping.StartSpecified || (mapping.Start + mapping.Offset) <= tvdb.Index;
                var endInRange = !mapping.EndSpecified || (mapping.End + mapping.Offset) >= tvdb.Index;

                if (startInRange && endInRange)
                    return tvdb.Index - mapping.Offset;
            }

            return null;
        }

        private int? FindAnidbEpisodeMapping(AnidbEpisode anidb, AnimelistMapping mapping)
        {
            var maps = GetEpisodeMappings(mapping);
            var exact = maps.FirstOrDefault(x => x.Anidb == anidb.Index);

            if (exact != null)
                return exact.Tvdb;

            if (mapping.OffsetSpecified)
            {
                var startInRange = !mapping.StartSpecified || mapping.Start <= anidb.Index;
                var endInRange = !mapping.EndSpecified || mapping.End >= anidb.Index;

                if (startInRange && endInRange)
                    return anidb.Index + mapping.Offset;
            }

            return null;
        }

        private List<EpisodeMapping> GetEpisodeMappings(AnimelistMapping mapping)
        {
            if (mapping.ParsedMappings == null)
            {
                var pairs = mapping.Value.Split(';');
                mapping.ParsedMappings = pairs
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(x =>
                    {
                        var parts = x.Split('-');
                        return new EpisodeMapping
                        {
                            Anidb = int.Parse(parts[0]),
                            Tvdb = int.Parse(parts[1])
                        };
                    }).ToList();
            }

            return mapping.ParsedMappings;
        }
    }
}