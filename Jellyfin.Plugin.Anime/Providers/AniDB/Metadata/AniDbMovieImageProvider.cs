using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Anime.Providers.AniDB.Metadata
{
    /// <summary>
    /// Provides movie images fetched from AniDB. Interally, this class uses AniDbSeriesImagesProvider
    /// as AniDB does not differentiate between shows and movies.
    /// </summary>
    public class AniDbMovieImageProvider : IRemoteImageProvider
    {
        public string Name => "AniDB";
        private readonly AniDbSeriesImagesProvider _seriesImagesProvider;

        public AniDbMovieImageProvider(IApplicationPaths appPaths, IHttpClient httpClient)
        {
            _seriesImagesProvider = new AniDbSeriesImagesProvider(httpClient, appPaths);
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _seriesImagesProvider.GetImageResponse(url, cancellationToken);
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var movie = (Movie)item;
            var seriesId = movie.ProviderIds.GetOrDefault(ProviderNames.AniDb);
            if (string.IsNullOrEmpty(seriesId))
            {
                return Enumerable.Empty<RemoteImageInfo>();
            }

            return await _seriesImagesProvider.GetImages(seriesId, cancellationToken);
        }

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return _seriesImagesProvider.GetSupportedImages(item);
        }

        public bool Supports(BaseItem item)
        {
            return item is Movie;
        }
    }
}
