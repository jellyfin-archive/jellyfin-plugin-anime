using System;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Plugins.Anime.Providers.AniDB.Identity;

namespace MediaBrowser.Plugins.Anime.Providers.AniList
{
    //public class AniListSeriesIdentityProvider : IItemIdentityProvider<SeriesInfo>
    //{
    //    private readonly AniListApiClient _api;

    //    public AniListSeriesIdentityProvider(IHttpClient http, ILogManager logManager, IJsonSerializer jsonSerializer)
    //    {
    //        _api = new AniListApiClient(http, logManager, jsonSerializer);
    //    }

    //    public async Task Identify(SeriesInfo info)
    //    {
    //        if (!string.IsNullOrEmpty(info.ProviderIds.GetOrDefault(ProviderNames.AniList)) )
    //            return;

    //        if (string.IsNullOrEmpty(info.Name))
    //            return;

    //        try
    //        {
    //            var search = await _api.Search(info.Name);

    //            var cleaned = AniDbTitleMatcher.GetComparableName(info.Name);
    //            if (!search.Any() && String.Compare(info.Name, cleaned, StringComparison.OrdinalIgnoreCase) != 0)
    //                search = await _api.Search(cleaned);

    //            var first = search.FirstOrDefault();

    //            if (first == null)
    //                return;
                
    //            info.ProviderIds.Remove(ProviderNames.AniList);
    //            info.ProviderIds.Add(ProviderNames.AniList, first.id.ToString());
    //        }
    //        catch (Exception e)
    //        {
    //            System.Diagnostics.Debug.WriteLine(e);
    //            // ignore
    //        }
    //    }
    //}
}