namespace MediaBrowser.Plugins.Anime.Providers.AniDB.Identity
{
    //public class AnidbSeriesIdentityProvider : IItemIdentityProvider<SeriesInfo>
    //{
    //    public async Task Identify(SeriesInfo info)
    //    {
    //        if (info.ProviderIds.ContainsKey(ProviderNames.AniDb))
    //            return;

    //        var aid = await AniDbTitleMatcher.DefaultInstance.FindSeries(info.Name, CancellationToken.None);
    //        if (!string.IsNullOrEmpty(aid))
    //        {
    //            info.ProviderIds.Remove(ProviderNames.AniDb);
    //            info.ProviderIds.Add(ProviderNames.AniDb, aid);
    //        }
    //    }
    //}
}