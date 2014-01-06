using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;

namespace MediaBrowser.Plugins.Anime.Providers
{
    public interface ISeriesProvider
    {
        bool RequiresInternet { get; }
        bool NeedsRefreshBasedOnCompareDate(BaseItem item, BaseProviderInfo providerInfo);
        Task<SeriesInfo> FindSeriesInfo(Dictionary<string, string> providerIds, string preferredMetadataLanguage, CancellationToken cancellationToken);
    }
}
