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
using MediaBrowser.Plugins.Anime.Providers.AniList.MediaBrowser.Plugins.Anime.Providers.AniList;
using MediaBrowser.Model.Serialization;
//API v2
namespace MediaBrowser.Plugins.Anime.Providers.AniList
{
    public class AniListSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        private readonly IHttpClient _httpClient;
        private readonly IApplicationPaths _paths;
        private readonly ILogger _log;
        private readonly api _api;
        public int Order => -2;
        public string Name => "AniList";
        public static readonly SemaphoreSlim ResourcePool = new SemaphoreSlim(1, 1);

        public AniListSeriesProvider(IApplicationPaths appPaths, IHttpClient httpClient, ILogManager logManager, IJsonSerializer jsonSerializer)
        {
            _log = logManager.GetLogger("AniList");
            _httpClient = httpClient;
            _api = new api(jsonSerializer);
            _paths = appPaths;
        }

        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>();

            var aid = info.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (string.IsNullOrEmpty(aid))
            {
                _log.Info("Start AniList... Searching(" + info.Name + ")");
                aid = await _api.FindSeries(info.Name, cancellationToken);
            }

            if (!string.IsNullOrEmpty(aid))
            {
                RootObject  WebContent = await _api.WebRequestAPI(_api.AniList_anime_link.Replace("{0}",aid));
                result.Item = new Series();
                result.HasMetadata = true;
               
                result.People = await _api.getPersonInfo(WebContent.data.Media.id);
                result.Item.ProviderIds.Add(ProviderNames.AniList, aid);
                result.Item.Overview = WebContent.data.Media.description;
                try
                {
                    //AniList has a max rating of 5
                    result.Item.CommunityRating = (WebContent.data.Media.averageScore/10);
                }
                catch (Exception) { }
                foreach (var genre in _api.Get_Genre(WebContent))
                    result.Item.AddGenre(genre);
                GenreHelper.CleanupGenres(result.Item);
                StoreImageUrl(aid, WebContent.data.Media.coverImage.large, "image");
            }
            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new Dictionary<string, RemoteSearchResult>();

            var aid = searchInfo.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (!string.IsNullOrEmpty(aid))
            {
                if (!results.ContainsKey(aid))
                    results.Add(aid, await _api.GetAnime(aid));
            }

            if (!string.IsNullOrEmpty(searchInfo.Name))
            {
                List<string> ids = await _api.Search_GetSeries_list(searchInfo.Name, cancellationToken);
                foreach (string a in ids)
                {
                    results.Add(a, await _api.GetAnime(a));
                }
            }

            return results.Values;
        }

        private void StoreImageUrl(string series, string url, string type)
        {
            var path = Path.Combine(_paths.CachePath, "anilist", type, series + ".txt");
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

    public class AniListSeriesImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClient _httpClient;
        private readonly IApplicationPaths _appPaths;
        private readonly api _api;
        public AniListSeriesImageProvider(IHttpClient httpClient, IApplicationPaths appPaths, IJsonSerializer jsonSerializer)
        {
            _httpClient = httpClient;
            _appPaths = appPaths;
            _api = new api(jsonSerializer);
        }

        public string Name => "AniList";

        public bool Supports(BaseItem item) => item is Series || item is Season;

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new[] { ImageType.Primary };
        }

        public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var seriesId = item.GetProviderId(ProviderNames.AniList);
            return GetImages(seriesId, cancellationToken);
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(string aid, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();

            if (!string.IsNullOrEmpty(aid))
            {
                var primary =  _api.Get_ImageUrl(await _api.WebRequestAPI(_api.AniList_anime_link.Replace("{0}", aid)));
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
                ResourcePool = AniListSeriesProvider.ResourcePool
            });
        }
    }
}