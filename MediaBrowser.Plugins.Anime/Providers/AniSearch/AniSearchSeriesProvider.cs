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

namespace MediaBrowser.Plugins.Anime.Providers.AniSearch
{
    public class AniSearchSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        private readonly IHttpClient _httpClient;
        private readonly IApplicationPaths _paths;
        private readonly ILogger _log;
        public int Order => -3;
        public string Name => "AniSearch";
        public static readonly SemaphoreSlim ResourcePool = new SemaphoreSlim(1, 1);

        public AniSearchSeriesProvider(IApplicationPaths appPaths, IHttpClient httpClient, ILogManager logManager)
        {
            _log = logManager.GetLogger("AniSearch");
            _httpClient = httpClient;
            _paths = appPaths;
        }

        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>();

            var aid = info.ProviderIds.GetOrDefault(ProviderNames.AniSearch);
            if (string.IsNullOrEmpty(aid))
            {
                _log.Info("Start AniSearch... Searching(" + info.Name + ")");
                aid = await Api.FindSeries(info.Name, cancellationToken);
            }

            if (!string.IsNullOrEmpty(aid))
            {
                string WebContent = await Api.WebRequestAPI(Api.AniSearch_anime_link + aid);
                result.Item = new Series();
                result.HasMetadata = true;

                result.Item.ProviderIds.Add(ProviderNames.AniSearch, aid);
                result.Item.Overview = await Api.Get_Overview(WebContent);
                try
                {
                    //AniSearch has a max rating of 5
                    result.Item.CommunityRating = (float.Parse(await Api.Get_Rating(WebContent), System.Globalization.CultureInfo.InvariantCulture) * 2);
                }
                catch (Exception) { }
                foreach (var genre in await Api.Get_Genre(WebContent))
                    result.Item.AddGenre(genre);
                GenreHelper.CleanupGenres(result.Item);
                StoreImageUrl(aid, await Api.Get_ImageUrl(WebContent), "image");
            }
            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new Dictionary<string, RemoteSearchResult>();

            var aid = searchInfo.ProviderIds.GetOrDefault(ProviderNames.AniSearch);
            if (!string.IsNullOrEmpty(aid))
            {
                if (!results.ContainsKey(aid))
                    results.Add(aid, await Api.GetAnime(aid));
            }

            if (!string.IsNullOrEmpty(searchInfo.Name))
            {
                List<string> ids = await Api.Search_GetSeries_list(searchInfo.Name, cancellationToken);
                foreach (string a in ids)
                {
                    results.Add(a, await Api.GetAnime(a));
                }
            }

            return results.Values;
        }

        private void StoreImageUrl(string series, string url, string type)
        {
            var path = Path.Combine(_paths.CachePath, "anisearch", type, series + ".txt");
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

    public class AniSearchSeriesImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClient _httpClient;
        private readonly IApplicationPaths _appPaths;

        public AniSearchSeriesImageProvider(IHttpClient httpClient, IApplicationPaths appPaths)
        {
            _httpClient = httpClient;
            _appPaths = appPaths;
        }

        public string Name => "AniSearch";

        public bool Supports(BaseItem item) => item is Series || item is Season;

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new[] { ImageType.Primary };
        }

        public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var seriesId = item.GetProviderId(ProviderNames.AniSearch);
            return GetImages(seriesId, cancellationToken);
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(string aid, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();

            if (!string.IsNullOrEmpty(aid))
            {
                var primary = await Api.Get_ImageUrl(await Api.WebRequestAPI(Api.AniSearch_anime_link + aid));
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
                ResourcePool = AniSearchSeriesProvider.ResourcePool
            });
        }
    }
}