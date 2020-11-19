using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Net.Http;
using Jellyfin.Plugin.Anime.Providers.KitsuIO.ApiClient;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Anime.Providers.KitsuIO.Metadata
{
    public class KitsuIoSeriesProvider : IRemoteMetadataProvider<MediaBrowser.Controller.Entities.TV.Series, SeriesInfo>, IHasOrder
    {
        private readonly ILogger<KitsuIoSeriesProvider> _log;
        private readonly IApplicationPaths _paths;
        public int Order => -4;
        public string Name => ProviderNames.KitsuIo;

        public KitsuIoSeriesProvider(ILogger<KitsuIoSeriesProvider> logger, IApplicationPaths paths)
        {
            _log = logger;
            _paths = paths;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            var filters = GetFiltersFromSeriesInfo(searchInfo);
            var searchResults = await KitsuIoApi.Search_Series(filters);
            var results = new List<RemoteSearchResult>();

            foreach (var series in searchResults.Data)
            {
                var parsedSeries = new RemoteSearchResult
                {
                    Name = series.Attributes.Titles.GetTitle,
                    SearchProviderName = Name,
                    ImageUrl = series.Attributes.PosterImage.Medium.ToString(),
                    Overview = series.Attributes.Synopsis,
                    ProductionYear = series.Attributes.StartDate?.Year,
                    PremiereDate = series.Attributes.StartDate?.DateTime,
                };
                parsedSeries.SetProviderId(ProviderNames.KitsuIo, series.Id.ToString());
                results.Add(parsedSeries);
            }

            return results;
        }

        public async Task<MetadataResult<MediaBrowser.Controller.Entities.TV.Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<MediaBrowser.Controller.Entities.TV.Series>();

            var kitsuId = info.ProviderIds.GetOrDefault(ProviderNames.KitsuIo);
            if (string.IsNullOrEmpty(kitsuId))
            {
                _log.LogInformation("Start KitsuIo... Searching({Name})", info.Name);
                var filters = GetFiltersFromSeriesInfo(info);
                var apiResponse = await KitsuIoApi.Search_Series(filters);
                kitsuId = apiResponse.Data.FirstOrDefault(x => x.Attributes.Titles.Equal(info.Name))?.Id.ToString();
            }

            if (!string.IsNullOrEmpty(kitsuId))
            {
                var seriesInfo = await KitsuIoApi.Get_Series(kitsuId);
                result.HasMetadata = true;
                result.Item = new MediaBrowser.Controller.Entities.TV.Series
                {
                    Overview = seriesInfo.Data.Attributes.Synopsis,
                    // KitsuIO has a max rating of 100
                    CommunityRating = string.IsNullOrWhiteSpace(seriesInfo.Data.Attributes.AverageRating)
                        ? null
                        : (float?) float.Parse(seriesInfo.Data.Attributes.AverageRating, System.Globalization.CultureInfo.InvariantCulture) / 10,
                    ProviderIds = new Dictionary<string, string>() {{ProviderNames.KitsuIo, kitsuId}},
                    Genres = seriesInfo.Included?.Select(x => x.Attributes.Name).ToArray()
                             ?? Array.Empty<string>()
                };
                GenreHelper.CleanupGenres(result.Item);
                StoreImageUrl(kitsuId, seriesInfo.Data.Attributes.PosterImage.Original.ToString(), "image");
            }

            return result;
        }

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var httpClient = Plugin.Instance.GetHttpClient();

            return await httpClient.GetAsync(url).ConfigureAwait(false);
        }

        private Dictionary<string, string> GetFiltersFromSeriesInfo(SeriesInfo seriesInfo)
        {
            var filters = new Dictionary<string, string> {{"text", HttpUtility.UrlEncode(seriesInfo.Name)}};
            if(seriesInfo.Year.HasValue) filters.Add("seasonYear", HttpUtility.UrlEncode(seriesInfo.Year.ToString()));
            return filters;
        }

        private void StoreImageUrl(string series, string url, string type)
        {
            var path = Path.Combine(_paths.CachePath, "kitsu", type, series + ".txt");
            var directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);

            File.WriteAllText(path, url);
        }
    }
}
