using System.Threading.Tasks;
using AnimeLists;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Plugins.Anime.Configuration;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB.Converter
{
    public class AnidbTvdbEpisodeConverter : IItemIdentityConverter<EpisodeInfo>
    {
        private readonly Mapper _mapper;

        public AnidbTvdbEpisodeConverter()
        {
            _mapper = AnidbConverter.DefaultInstance.Mapper;
        }

        public async Task<bool> Convert(EpisodeInfo info)
        {
            var anidb = info.ProviderIds.GetOrDefault(ProviderNames.AniDb);
            var tvdb = info.ProviderIds.GetOrDefault("Tvdb-Full");

            if (string.IsNullOrEmpty(anidb) && !string.IsNullOrEmpty(tvdb))
            {
                var converted = TvdbToAnidb(tvdb);
                if (converted != null)
                {
                    info.ProviderIds.Add(ProviderNames.AniDb, converted);
                    return true;
                }
            }

            var overrideTvdb = string.IsNullOrEmpty(tvdb) 
                || info.ParentIndexNumber == null
                || (info.ParentIndexNumber < 2 && PluginConfiguration.Instance().UseAnidbOrderingWithSeasons);

            if (!string.IsNullOrEmpty(anidb) && overrideTvdb)
            {
                var converted = AnidbToTvdb(anidb);
                if (converted != null && converted != tvdb)
                {
                    info.ProviderIds["Tvdb-Full"] = converted;
                    return tvdb != converted;
                }
            }

            return false;
        }

        private string TvdbToAnidb(string tvdb)
        {
            var tvdbId = TvdbEpisodeIdentity.Parse(tvdb);
            if (tvdbId == null)
                return null;

            var converted = _mapper.ToAnidb(new TvdbEpisode
            {
                Series = tvdbId.Value.SeriesId,
                Season = tvdbId.Value.SeasonIndex,
                Index = tvdbId.Value.EpisodeNumber
            });

            if (converted == null)
                return null;

            int? end = null;
            if (tvdbId.Value.EpisodeNumberEnd != null)
            {
                var convertedEnd = _mapper.ToAnidb(new TvdbEpisode
                {
                    Series = tvdbId.Value.SeriesId,
                    Season = tvdbId.Value.SeasonIndex,
                    Index = tvdbId.Value.EpisodeNumberEnd.Value
                });

                if (convertedEnd != null && convertedEnd.Season == converted.Season)
                    end = convertedEnd.Index;
            }

            var id = new AnidbEpisodeIdentity(converted.Series, converted.Index, end, null);
            return id.ToString();
        }

        private string AnidbToTvdb(string anidb)
        {
            var anidbId = AnidbEpisodeIdentity.Parse(anidb);
            if (anidbId == null)
                return null;

            var converted = _mapper.ToTvdb(new AnidbEpisode
            {
                Series = anidbId.Value.SeriesId,
                Season = string.IsNullOrEmpty(anidbId.Value.EpisodeType) ? 1 : 0,
                Index = anidbId.Value.EpisodeNumber
            });

            int? end = null;
            if (anidbId.Value.EpisodeNumberEnd != null)
            {
                var convertedEnd = _mapper.ToAnidb(new TvdbEpisode
                {
                    Series = anidbId.Value.SeriesId,
                    Season = string.IsNullOrEmpty(anidbId.Value.EpisodeType) ? 1 : 0,
                    Index = anidbId.Value.EpisodeNumberEnd.Value
                });

                if (convertedEnd.Season == converted.Season)
                    end = convertedEnd.Index;
            }

            var id = new TvdbEpisodeIdentity(converted.Series, converted.Season, converted.Index, end);
            return id.ToString();
        }
    }
}