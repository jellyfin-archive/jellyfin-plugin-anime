using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB.Metadata
{
    /// <summary>
    ///     Copies the series image into a season, if the season does not otherwise have any primary image.
    /// </summary>
    public class AniDbSeasonImageProvider : IRemoteImageProvider
    {
        private readonly IApplicationPaths _appPaths;
        private readonly IHttpClient _httpClient;

        public AniDbSeasonImageProvider(IApplicationPaths appPaths, IHttpClient httpClient)
        {
            _appPaths = appPaths;
            _httpClient = httpClient;
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return new AniDbSeriesImagesProvider(_httpClient, _appPaths).GetImageResponse(url, cancellationToken);
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var season = (Season)item;
            var series = season.Series;

            var seriesId = series.ProviderIds.GetOrDefault(ProviderNames.AniDb);
            if (string.IsNullOrEmpty(seriesId))
                return Enumerable.Empty<RemoteImageInfo>();

            return await new AniDbSeriesImagesProvider(_httpClient, _appPaths).GetImages(seriesId, cancellationToken);
        }

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new[] { ImageType.Primary };
        }

        public string Name => "AniDB";

        public bool Supports(BaseItem item)
        {
            return item is Season;
        }
    }
}