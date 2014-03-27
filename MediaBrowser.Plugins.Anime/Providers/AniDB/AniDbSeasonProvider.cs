using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    public class AniDbSeasonProvider : IRemoteMetadataProvider<Season, SeasonInfo>
    {
        private readonly SeriesIndexSearch _indexSearcher;
        private readonly AniDbSeriesProvider _seriesProvider;

        public AniDbSeasonProvider(IServerConfigurationManager configurationManager, IHttpClient httpClient, IApplicationPaths appPaths, ILogManager logManager)
        {
            _indexSearcher = new SeriesIndexSearch(configurationManager, httpClient);
            _seriesProvider = new AniDbSeriesProvider(appPaths, httpClient, logManager);
        }

        public async Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Season>();
            
            string seriesId = info.SeriesProviderIds.GetOrDefault(ProviderNames.AniDb);
            if (seriesId == null)
                return result;

            string seasonid = await _indexSearcher.FindSeriesByRelativeIndex(seriesId, (info.IndexNumber ?? 1) - 1, cancellationToken).ConfigureAwait(false);

            var seriesInfo = new SeriesInfo();
            seriesInfo.ProviderIds.Add(ProviderNames.AniDb, seasonid);

            var seriesResult = await _seriesProvider.GetMetadata(seriesInfo, cancellationToken);
            if (seriesResult.HasMetadata)
            {
                result.HasMetadata = true;
                result.Item = new Season
                {
                    IndexNumber = info.IndexNumber,
                    Name = seriesResult.Item.Name,
                    Overview = seriesResult.Item.Overview,
                    PremiereDate = seriesResult.Item.PremiereDate,
                    EndDate = seriesResult.Item.EndDate,
                    CommunityRating = seriesResult.Item.CommunityRating,
                    VoteCount = seriesResult.Item.VoteCount,
                    People = seriesResult.Item.People,
                    Studios = seriesResult.Item.Studios,
                    Genres = seriesResult.Item.Genres
                };
            }

            return result;
        }

        public string Name
        {
            get { return "AniDB"; }
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}