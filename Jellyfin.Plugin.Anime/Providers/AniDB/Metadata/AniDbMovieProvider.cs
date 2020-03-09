using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Anime.Providers.AniDB.Metadata
{
    public class AniDbMovieProvider : IRemoteMetadataProvider<Movie, MovieInfo>
    {
        private readonly AniDbSeriesProvider _seriesProvider;
        private readonly ILogger _log;

        public string Name => "AniDB";

        public AniDbMovieProvider(IApplicationPaths appPaths, IHttpClient httpClient, ILoggerFactory loggerFactory)
        {
            _seriesProvider = new AniDbSeriesProvider(appPaths, httpClient);
            _log = loggerFactory.CreateLogger("AniDB");
        }

        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            // Empty result
            var result = new MetadataResult<Movie>
            {
                HasMetadata = false,
                Item = new Movie
                {
                    Name = info.Name
                }
            };

            var seriesId = info.ProviderIds.GetOrDefault(ProviderNames.AniDb);
            if (seriesId == null)
            {
                return result;
            }

            var seriesInfo = new SeriesInfo();
            seriesInfo.ProviderIds.Add(ProviderNames.AniDb, seriesId);

            var seriesResult = await _seriesProvider.GetMetadata(seriesInfo, cancellationToken);
            if (seriesResult.HasMetadata)
            {
                result = new MetadataResult<Movie>
                {
                    HasMetadata = true,
                    Item = new Movie
                    {
                        Name = seriesResult.Item.Name,
                        Overview = seriesResult.Item.Overview,
                        PremiereDate = seriesResult.Item.PremiereDate,
                        ProductionYear = seriesResult.Item.ProductionYear,
                        EndDate = seriesResult.Item.EndDate,
                        CommunityRating = seriesResult.Item.CommunityRating,
                        Studios = seriesResult.Item.Studios,
                        Genres = seriesResult.Item.Genres,
                        ProviderIds = seriesResult.Item.ProviderIds,
                    },
                    People = seriesResult.People,
                    Images = seriesResult.Images
                };
            }

            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        {
            var seriesInfo = new SeriesInfo();
            var seriesId = searchInfo.ProviderIds.GetOrDefault(ProviderNames.AniDb);
            if (seriesId != null)
            {
                seriesInfo.ProviderIds.Add(ProviderNames.AniDb, seriesId);
            }
            seriesInfo.Name = searchInfo.Name;

            return await _seriesProvider.GetSearchResults(seriesInfo, cancellationToken);
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _seriesProvider.GetImageResponse(url, cancellationToken);
        }
    }
}
