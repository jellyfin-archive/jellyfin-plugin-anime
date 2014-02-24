using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Plugins.Anime.Providers.AniDB;

namespace MediaBrowser.Plugins.Anime.Providers
{
    /// <summary>
    /// The SeriesIndexProvider class is a metadata provider which finds the absolute series index of an anime series via AniDB data.
    /// </summary>
    public class SeriesIndexProvider
        : IRemoteMetadataProvider<Series, MediaBrowser.Controller.Providers.SeriesInfo>
    {
        private readonly IAniDbTitleMatcher _titleMatcher;
        private readonly SeriesIndexSearch _indexSearcher;
        private readonly ILogger _log;

        public SeriesIndexProvider(IAniDbTitleMatcher titleMatcher, ILogManager logManager, IServerConfigurationManager configurationManager, IHttpClient httpClient)
        {
            _titleMatcher = titleMatcher;
            _log = logManager.GetLogger("Anime");
            _indexSearcher = new SeriesIndexSearch(configurationManager, httpClient);
        }

        public async Task<MetadataResult<Series>> GetMetadata(Controller.Providers.SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>();

            var aniDbId = await FindAniDbId(info, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(aniDbId))
            {
                result.Item = new Series();
                result.HasMetadata = true;

                result.Item.ProviderIds.Add(ProviderNames.AniDb, aniDbId);
                result.Item.IndexNumber = await _indexSearcher.FindSeriesIndex(aniDbId, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }

        public string Name
        {
            get { return "Anime"; }
        }

        private async Task<string> FindAniDbId(MediaBrowser.Controller.Providers.SeriesInfo series, CancellationToken cancellationToken)
        {
            string aid = series.ProviderIds.GetOrDefault(ProviderNames.AniDb);
            if (string.IsNullOrEmpty(aid))
            {
                aid = await _titleMatcher.FindSeries(series.Name, cancellationToken);

                if (!string.IsNullOrEmpty(aid))
                    _log.Debug("Identified {0} as AniDB ID {1}", series.Name, aid);
            }

            return aid;
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(Controller.Providers.SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}
