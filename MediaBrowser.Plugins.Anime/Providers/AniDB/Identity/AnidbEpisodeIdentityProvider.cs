namespace MediaBrowser.Plugins.Anime.Providers.AniDB.Identity
{
    //public class AnidbEpisodeIdentityProvider : IItemIdentityProvider<EpisodeInfo>
    //{
    //    public async Task Identify(EpisodeInfo info)
    //    {
    //        if (info.ProviderIds.ContainsKey(ProviderNames.AniDb))
    //            return;

    //        var inspectSeason = info.ParentIndexNumber == null ||
    //                            (info.ParentIndexNumber < 2 && Plugin.Instance.Configuration.UseAnidbOrderingWithSeasons);

    //        var series = info.SeriesProviderIds.GetOrDefault(ProviderNames.AniDb);
    //        if (!string.IsNullOrEmpty(series) && inspectSeason && info.IndexNumber != null)
    //        {
    //            string type = null;
    //            if (info.ParentIndexNumber != null)
    //            {
    //                type = info.ParentIndexNumber == 0 ? "S" : null;
    //            }

    //            var id = new AnidbEpisodeIdentity(series, info.IndexNumber.Value, info.IndexNumberEnd, type);
    //            info.ProviderIds.Remove(ProviderNames.AniDb);
    //            info.ProviderIds.Add(ProviderNames.AniDb, id.ToString());
    //        }
    //    }
    //}
}