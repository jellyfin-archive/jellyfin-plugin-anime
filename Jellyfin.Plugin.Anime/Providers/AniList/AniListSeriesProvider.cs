using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

//API v2
namespace Jellyfin.Plugin.Anime.Providers.AniList
{
    public class AniListSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        private readonly IHttpClient _httpClient;
        private readonly IApplicationPaths _paths;
        private readonly ILogger _log;
        private readonly AniListApi _aniListApi;
        public int Order => -2;
        public string Name => "AniList";

        public AniListSeriesProvider(IApplicationPaths appPaths, IHttpClient httpClient, ILogger<AniListSeriesProvider> logger, IJsonSerializer jsonSerializer)
        {
            _log = logger;
            _httpClient = httpClient;
            _aniListApi = new AniListApi(jsonSerializer);
            _paths = appPaths;
        }

        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>();

            var aid = info.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (string.IsNullOrEmpty(aid))
            {
                _log.LogInformation("Start AniList... Searching({Name})", info.Name);
                aid = await _aniListApi.FindSeries(info.Name, cancellationToken);
            }

            if (!string.IsNullOrEmpty(aid))
            {
                RootObject WebContent = await _aniListApi.WebRequestAPI(_aniListApi.AniList_anime_link.Replace("{0}", aid));
                result.Item = new Series();
                result.HasMetadata = true;
               
                result.People = await _aniListApi.GetPersonInfo(WebContent.data.Media.id, cancellationToken);
                result.Item.ProviderIds.Add(ProviderNames.AniList, aid);
                result.Item.Overview = WebContent.data.Media.description;
                try
                {
                    //AniList has a max rating of 5
                    result.Item.CommunityRating = (WebContent.data.Media.averageScore/10);
                }
                catch (Exception) { }
                foreach (var genre in _aniListApi.Get_Genre(WebContent))
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
                {
                    results.Add(aid, await _aniListApi.GetAnime(aid).ConfigureAwait(false));
                }
            }

            if (!string.IsNullOrEmpty(searchInfo.Name))
            {
                List<string> ids = await _aniListApi.Search_GetSeries_list(searchInfo.Name, cancellationToken).ConfigureAwait(false);
                foreach (string a in ids)
                {
                    results.Add(a, await _aniListApi.GetAnime(a).ConfigureAwait(false));
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
                UserAgent = Constants.UserAgent,
                CancellationToken = cancellationToken,
                Url = url
            });
        }
    }

    public class AniListSeriesImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClient _httpClient;
        private readonly IApplicationPaths _appPaths;
        private readonly AniListApi _aniListApi;
        public AniListSeriesImageProvider(IHttpClient httpClient, IApplicationPaths appPaths, IJsonSerializer jsonSerializer)
        {
            _httpClient = httpClient;
            _appPaths = appPaths;
            _aniListApi = new AniListApi(jsonSerializer);
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
                var primary =  _aniListApi.Get_ImageUrl(await _aniListApi.WebRequestAPI(_aniListApi.AniList_anime_link.Replace("{0}", aid)));
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
                UserAgent = Constants.UserAgent,
                CancellationToken = cancellationToken,
                Url = url
            });
        }
    }
}
