using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    /// <summary>
    ///     Copies the series image into a season, if the season does not otherwise have any primary image.
    /// </summary>
    public class AniDbSeasonImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClient _httpClient;
        private readonly IApplicationPaths _appPaths;

        public AniDbSeasonImageProvider(IApplicationPaths appPaths, IHttpClient httpClient)
        {
            _appPaths = appPaths;
            _httpClient = httpClient;
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return new AniDbSeriesImagesProvider(_httpClient, _appPaths).GetImageResponse(url, cancellationToken);
        }

        public Task<IEnumerable<RemoteImageInfo>> GetImages(IHasImages item, CancellationToken cancellationToken)
        {
            var season = (Season)item;
            var series = season.Series;

            return new AniDbSeriesImagesProvider(_httpClient, _appPaths).GetImages(series, cancellationToken);
        }

        public IEnumerable<ImageType> GetSupportedImages(IHasImages item)
        {
            return new[] { ImageType.Primary };
        }

        public string Name
        {
            get { return "AniDB"; }
        }

        public bool Supports(IHasImages item)
        {
            return item is Season;
        }
    }
}