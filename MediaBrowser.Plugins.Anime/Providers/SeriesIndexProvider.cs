using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugins.Anime.Providers.AniDB;

namespace MediaBrowser.Plugins.Anime.Providers
{
    /// <summary>
    ///     The SeriesIndexProvider class is a metadata provider which finds the absolute series index of an anime series via
    ///     AniDB data.
    /// </summary>
    public class SeriesIndexProvider
        : ICustomMetadataProvider<Series>, IPreRefreshProvider
    {
        private readonly SeriesIndexSearch _indexSearcher;
        private readonly ILogger _log;
        private readonly IAniDbTitleMatcher _titleMatcher;

        public SeriesIndexProvider(ILogManager logManager, IServerConfigurationManager configurationManager, IHttpClient httpClient)
        {
            _titleMatcher = AniDbTitleMatcher.DefaultInstance;
            _log = logManager.GetLogger("Anime");
            _indexSearcher = new SeriesIndexSearch(configurationManager, httpClient);
        }

        public async Task<ItemUpdateType> FetchAsync(Series item, IDirectoryService directoryService, CancellationToken cancellationToken)
        {
            string aniDbId = await FindAniDbId(item, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(aniDbId))
            {
                item.ProviderIds.Add(ProviderNames.AniDb, aniDbId);
                item.AnimeSeriesIndex = await _indexSearcher.FindSeriesIndex(aniDbId, cancellationToken).ConfigureAwait(false);

                return ItemUpdateType.MetadataImport;
            }

            return ItemUpdateType.None;
        }

        public string Name
        {
            get { return "Anime"; }
        }

        private async Task<string> FindAniDbId(Series series, CancellationToken cancellationToken)
        {
            string aid = series.ProviderIds.GetOrDefault(ProviderNames.AniDb);
            if (string.IsNullOrEmpty(aid))
                aid = await _titleMatcher.FindSeries(series.Name, cancellationToken);

            return aid;
        }
    }

    public class SeriesOrderProvider
        : ISeriesOrderProvider
    {
        private readonly SeriesIndexSearch _indexSearcher;
        private readonly IAniDbTitleMatcher _titleMatcher;

        public SeriesOrderProvider(IServerConfigurationManager configurationManager, IHttpClient httpClient)
        {
            _titleMatcher = AniDbTitleMatcher.DefaultInstance;
            _indexSearcher = new SeriesIndexSearch(configurationManager, httpClient);
        }

        public async Task<int?> FindSeriesIndex(string seriesName)
        {
            var cancellationSource = new CancellationTokenSource();
            string aniDbId = await _titleMatcher.FindSeries(seriesName, cancellationSource.Token);
            if (aniDbId == null)
                return null;

            return await _indexSearcher.FindSeriesIndex(aniDbId, cancellationSource.Token);
        }

        public string OrderType
        {
            get { return SeriesOrderTypes.Anime; }
        }
    }
}