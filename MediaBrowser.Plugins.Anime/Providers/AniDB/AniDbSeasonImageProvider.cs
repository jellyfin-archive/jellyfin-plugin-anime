using System.Linq;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
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
        private readonly SeriesIndexSearch _indexSearcher;
        private readonly IHttpClient _httpClient;
        private readonly IApplicationPaths _appPaths;

        public AniDbSeasonImageProvider(IServerConfigurationManager configurationManager,  IApplicationPaths appPaths, IHttpClient httpClient)
        {
            _appPaths = appPaths;
            _httpClient = httpClient;
            _indexSearcher = new SeriesIndexSearch(configurationManager, httpClient);
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return new AniDbSeriesImagesProvider(_httpClient, _appPaths).GetImageResponse(url, cancellationToken);
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(IHasImages item, CancellationToken cancellationToken)
        {
            var season = (Season)item;
            var series = season.Series;

            string seriesId = series.ProviderIds.GetOrDefault(ProviderNames.AniDb);
            if (string.IsNullOrEmpty(seriesId))
                return Enumerable.Empty<RemoteImageInfo>();

            string seasonid = await _indexSearcher.FindSeriesByRelativeIndex(seriesId, (season.IndexNumber ?? 1) - 1, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(seasonid))
                return Enumerable.Empty<RemoteImageInfo>();

            return await new AniDbSeriesImagesProvider(_httpClient, _appPaths).GetImages(seasonid, cancellationToken);
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