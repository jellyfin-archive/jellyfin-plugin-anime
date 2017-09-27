using System;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB.Converter
{
    public struct TvdbEpisodeIdentity
    {
        public string SeriesId { get; private set; }
        public int? SeasonIndex { get; private set; }
        public int EpisodeNumber { get; private set; }
        public int? EpisodeNumberEnd { get; private set; }

        public TvdbEpisodeIdentity(string id)
            : this()
        {
            this = Parse(id).Value;
        }

        public TvdbEpisodeIdentity(string seriesId, int? seasonIndex, int episodeNumber, int? episodeNumberEnd)
            : this()
        {
            SeriesId = seriesId;
            SeasonIndex = seasonIndex;
            EpisodeNumber = episodeNumber;
            EpisodeNumberEnd = episodeNumberEnd;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}:{2}",
                SeriesId,
                SeasonIndex != null ? SeasonIndex.Value.ToString() : "A",
                EpisodeNumber + (EpisodeNumberEnd != null ? "-" + EpisodeNumberEnd.Value.ToString() : ""));
        }

        public static TvdbEpisodeIdentity? Parse(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            try
            {
                var parts = id.Split(':');
                var series = parts[0];
                var season = parts[1] != "A" ? (int?)int.Parse(parts[1]) : null;

                int index;
                int? indexEnd;

                if (parts[2].Contains("-"))
                {
                    var split = parts[2].IndexOf("-", StringComparison.OrdinalIgnoreCase);
                    index = int.Parse(parts[2].Substring(0, split));
                    indexEnd = int.Parse(parts[2].Substring(split + 1));
                }
                else
                {
                    index = int.Parse(parts[2]);
                    indexEnd = null;
                }

                return new TvdbEpisodeIdentity(series, season, index, indexEnd);
            }
            catch
            {
                return null;
            }
        }
    }
}