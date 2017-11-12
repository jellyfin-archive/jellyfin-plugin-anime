using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;

namespace MediaBrowser.Plugins.Anime.Providers.Proxer
{
    public class ProxerSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        private readonly ILogger _log;
        private readonly IHttpClient _httpClient;
        private readonly IApplicationPaths _paths;
        public static string provider_name = ProviderNames.Proxer;
        public static readonly SemaphoreSlim ResourcePool = new SemaphoreSlim(1, 1);
        public int Order => -4;
        public string Name => "Proxer";
        public ProxerSeriesProvider(IApplicationPaths appPaths, IHttpClient httpClient, ILogManager logManager)
        {
            _log = logManager.GetLogger("Proxer");
            _httpClient = httpClient;
            _paths = appPaths;
        }
        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>();

            var aid = info.ProviderIds.GetOrDefault(provider_name);
            if (string.IsNullOrEmpty(aid))
            {
                _log.Info("Start Proxer... Searching(" + info.Name + ")");
                aid = await api.FindSeries(info.Name, cancellationToken);
            }

            if (!string.IsNullOrEmpty(aid))
            {
                string WebContent = await api.WebRequestAPI(api.Proxer_anime_link + aid);
                result.Item = new Series();
                result.HasMetadata = true;

                result.Item.ProviderIds.Add(provider_name, aid);
                result.Item.Overview = await api.Get_Overview(WebContent);
                result.ResultLanguage = "ger";
                try
                {
                    result.Item.CommunityRating = float.Parse(await api.Get_Rating(WebContent), System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception) { }
                foreach (var genre in await api.Get_Genre(WebContent))
                    result.Item.AddGenre(genre);
                GenreHelper.CleanupGenres(result.Item);
                StoreImageUrl(aid, await api.Get_ImageUrl(WebContent), "image");
            }
            return result;
        }
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new Dictionary<string, RemoteSearchResult>();

            var aid = searchInfo.ProviderIds.GetOrDefault(provider_name);
            if (!string.IsNullOrEmpty(aid))
            {
                if (!results.ContainsKey(aid))
                    results.Add(aid, await api.GetAnime(aid));
            }

            if (!string.IsNullOrEmpty(searchInfo.Name))
            {
                List<string> ids = await api.Search_GetSeries_list(searchInfo.Name, cancellationToken);
                foreach (string a in ids)
                {
                    results.Add(a, await api.GetAnime(a));
                }
            }

            return results.Values;
        }

        private void StoreImageUrl(string series, string url, string type)
        {
            var path = Path.Combine(_paths.CachePath, "proxer", type, series + ".txt");
            var directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);

            File.WriteAllText(path, url);
        }
        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClient.GetResponse(new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = url,
                ResourcePool = ResourcePool

            });
        }
    }
    public class ProxerSeriesImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClient _httpClient;
        private readonly IApplicationPaths _appPaths;

        public ProxerSeriesImageProvider(IHttpClient httpClient, IApplicationPaths appPaths)
        {
            _httpClient = httpClient;
            _appPaths = appPaths;
        }

        public string Name => "Proxer";

        public bool Supports(IHasMetadata item) => item is Series || item is Season;


        public IEnumerable<ImageType> GetSupportedImages(IHasMetadata item)
        {
            return new[] { ImageType.Primary };
        }

        public Task<IEnumerable<RemoteImageInfo>> GetImages(IHasMetadata item, CancellationToken cancellationToken)
        {
            var seriesId = item.GetProviderId(ProxerSeriesProvider.provider_name);
            return GetImages(seriesId, cancellationToken);
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(string aid, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();

            if (!string.IsNullOrEmpty(aid))
            {
                var primary = api.Get_ImageUrl(await api.WebRequestAPI(api.Proxer_anime_link + aid));
                list.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Primary,
                    Url = await primary
                });

            }
            return list;

        }
        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClient.GetResponse(new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = url,
                ResourcePool = ProxerSeriesProvider.ResourcePool
            });
        }
    }

}
