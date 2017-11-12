namespace MediaBrowser.Plugins.Anime.Providers.AniDB.Converter
{
    //public class AnidbTvdbSeasonConverter : IItemIdentityConverter<SeasonInfo>
    //{
    //    private readonly Mapper _mapper;

    //    public AnidbTvdbSeasonConverter()
    //    {
    //        _mapper = AnidbConverter.DefaultInstance.Mapper;
    //    }

    //    public async Task<bool> Convert(SeasonInfo info)
    //    {
    //        var anidb = info.ProviderIds.GetOrDefault(ProviderNames.AniDb);
    //        var tvdb = info.SeriesProviderIds.GetOrDefault("Tvdb");

    //        if (string.IsNullOrEmpty(anidb) && !string.IsNullOrEmpty(tvdb))
    //        {
    //            if (info.IndexNumber != null)
    //            {
    //                var converted = TvdbToAnidb(tvdb, info.IndexNumber.Value);
    //                if (converted != null)
    //                {
    //                    info.ProviderIds.Add(ProviderNames.AniDb, converted);
    //                    return true;
    //                }
    //            }
    //            else
    //            {
    //                var series = info.SeriesProviderIds.GetOrDefault(ProviderNames.AniDb);
    //                if (!string.IsNullOrEmpty(series))
    //                {
    //                    info.ProviderIds.Add(ProviderNames.AniDb, series);
    //                    return true;
    //                }
    //            }
    //        }

    //        return false;
    //    }

    //    private string TvdbToAnidb(string tvdb, int season)
    //    {
    //        var converted = _mapper.ToAnidb(new TvdbEpisode
    //        {
    //            Series = tvdb,
    //            Season = season,
    //            Index = 1
    //        });

    //        return converted?.Series;
    //    }
    //}
}