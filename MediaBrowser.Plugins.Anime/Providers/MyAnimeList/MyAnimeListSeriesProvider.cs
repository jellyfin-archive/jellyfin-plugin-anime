using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Plugins.Anime.Providers.MyAnimeList
{
    public class MyAnimeListSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        private readonly ILogger _log;
        private readonly IHttpClient _httpClient;
        private readonly IApplicationPaths _paths;
        private readonly Api _api;
        public static readonly SemaphoreSlim ResourcePool = new SemaphoreSlim(1, 1);
        public static string provider_name = ProviderNames.MyAnimeList;
        public int Order => -5;
        public string Name => "MyAnimeList";

        public MyAnimeListSeriesProvider(IApplicationPaths appPaths, IHttpClient httpClient, ILogManager logManager)
        {
            _api = new Api(logManager);
            _log = logManager.GetLogger("MyAnimeList");
            _httpClient = httpClient;
            _paths = appPaths;
        }

        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>();

            var aid = info.ProviderIds.GetOrDefault(provider_name);
            if (string.IsNullOrEmpty(aid))
            {
                _log.Info("Start MyAnimeList... Searching(" + info.Name + ")");
                aid = await _api.FindSeries(info.Name, cancellationToken);
            }

            if (!string.IsNullOrEmpty(aid))
            {
                string WebContent = await _api.WebRequestAPI(_api.anime_link + aid, cancellationToken);
                result.Item = new Series();
                result.HasMetadata = true;

                result.Item.ProviderIds.Add(provider_name, aid);
                result.Item.Overview = await _api.Get_OverviewAsync(WebContent);
                result.ResultLanguage = "eng";
                try
                {
                    result.Item.CommunityRating = float.Parse(await _api.Get_RatingAsync(WebContent), System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception) { }
                foreach (var genre in await _api.Get_GenreAsync(WebContent))
                {
                    if (!string.IsNullOrEmpty(genre))
                    {
                        result.Item.AddGenre(genre);
                    }
                }
                GenreHelper.CleanupGenres(result.Item);
                StoreImageUrl(aid, await _api.Get_ImageUrlAsync(WebContent), "image");
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
                    results.Add(aid, await _api.GetAnime(aid, cancellationToken));
            }
            if (!string.IsNullOrEmpty(searchInfo.Name))
            {
                List<string> ids = await _api.Search_GetSeries_list(searchInfo.Name, cancellationToken);
                foreach (string a in ids)
                {
                    results.Add(a, await _api.GetAnime(a, cancellationToken));
                }
            }

            return results.Values;
        }

        private void StoreImageUrl(string series, string url, string type)
        {
            var path = Path.Combine(_paths.CachePath, "myanimelist", type, series + ".txt");
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

    public class MyAnimeListSeriesImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClient _httpClient;
        private readonly IApplicationPaths _appPaths;
        private readonly Api _api;
        public static readonly SemaphoreSlim ResourcePool = new SemaphoreSlim(1, 1);

        public MyAnimeListSeriesImageProvider(IHttpClient httpClient, IApplicationPaths appPaths, ILogManager logManager)
        {
            _api = new Api(logManager);
            _httpClient = httpClient;
            _appPaths = appPaths;
        }

        public string Name => "MyAnimeList";

        public bool Supports(BaseItem item) => item is Series || item is Season;

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new[] { ImageType.Primary };
        }

        public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var seriesId = item.GetProviderId(MyAnimeListSeriesProvider.provider_name);
            return GetImages(seriesId, cancellationToken);
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(string aid, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();

            if (!string.IsNullOrEmpty(aid))
            {
                var primary = await _api.Get_ImageUrlAsync(await _api.WebRequestAPI(_api.anime_link + aid, cancellationToken));
                list.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Primary,
                    Url = primary
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
                ResourcePool = MyAnimeListSeriesProvider.ResourcePool
            });
        }
    }
}