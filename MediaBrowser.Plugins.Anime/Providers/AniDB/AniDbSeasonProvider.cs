using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    public class AniDbSeasonProvider : IRemoteMetadataProvider<Season, SeasonInfo>
    {
        private readonly SeriesIndexSearch _indexSearcher;
        private readonly AniDbSeriesProvider _seriesProvider;

        public AniDbSeasonProvider(IServerConfigurationManager configurationManager, IHttpClient httpClient, IApplicationPaths appPaths, ILibraryManager library)
        {
            _indexSearcher = new SeriesIndexSearch(configurationManager, httpClient);
            _seriesProvider = new AniDbSeriesProvider(appPaths, httpClient, configurationManager, library);
        }

        public async Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Season> {
                HasMetadata = true,
                Item = new Season {
                    Name = info.Name,
                    IndexNumber = info.IndexNumber
                }
            };
            
            string seriesId = info.SeriesProviderIds.GetOrDefault(ProviderNames.AniDb);
            if (seriesId == null)
                return result;

            string seasonid = await _indexSearcher.FindSeriesByRelativeIndex(seriesId, (info.IndexNumber ?? 1) - 1, cancellationToken).ConfigureAwait(false);
            if (seasonid == null)
                return result;

            var seriesInfo = new SeriesInfo();
            seriesInfo.ProviderIds.Add(ProviderNames.AniDb, seasonid);

            var seriesResult = await _seriesProvider.GetMetadata(seriesInfo, cancellationToken);
            if (seriesResult.HasMetadata)
            {
                result.Item.Name = seriesResult.Item.Name;
                result.Item.Overview = seriesResult.Item.Overview;
                result.Item.PremiereDate = seriesResult.Item.PremiereDate;
                result.Item.EndDate = seriesResult.Item.EndDate;
                result.Item.CommunityRating = seriesResult.Item.CommunityRating;
                result.Item.VoteCount = seriesResult.Item.VoteCount;
                result.Item.People = seriesResult.Item.People;
                result.Item.Studios = seriesResult.Item.Studios;
                result.Item.Genres = seriesResult.Item.Genres;
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