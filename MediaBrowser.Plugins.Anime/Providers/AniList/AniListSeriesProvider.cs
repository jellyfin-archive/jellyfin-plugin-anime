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
using MediaBrowser.Plugins.Anime.Configuration;
using MediaBrowser.Plugins.Anime.Providers.AniDB.Identity;
using MediaBrowser.Plugins.Anime.Providers.AniDB.Metadata;

namespace MediaBrowser.Plugins.Anime.Providers.AniList
{
    public class AniListSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        private readonly IApplicationPaths _paths;
        private readonly AniListApiClient _api;

        public int Order => -2;
        public string Name => "AniList";

        public AniListSeriesProvider(IHttpClient http, IApplicationPaths paths, ILogManager logManager)
        {
            _paths = paths;
            _api = new AniListApiClient(http, logManager);
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new Dictionary<string, RemoteSearchResult>();

            var aid = searchInfo.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (!string.IsNullOrEmpty(aid))
            {
                var anime = await _api.GetAnime(aid);
                if (anime != null && !results.ContainsKey(aid))
                    results.Add(aid, ToSearchResult(anime));
            }

            if (!string.IsNullOrEmpty(searchInfo.Name))
            {
                var search = await _api.Search(searchInfo.Name);
                foreach (var a in search)
                {
                    if (!results.ContainsKey(a.id.ToString()))
                        results.Add(a.id.ToString(), ToSearchResult(a));
                }

                var cleaned = AniDbTitleMatcher.GetComparableName(searchInfo.Name);
                if (String.Compare(cleaned, searchInfo.Name, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    search = await _api.Search(cleaned);
                    foreach (var a in search)
                    {
                        if (!results.ContainsKey(a.id.ToString()))
                            results.Add(a.id.ToString(), ToSearchResult(a));
                    }
                }
            }

            return results.Values;
        }

        private RemoteSearchResult ToSearchResult(Anime anime)
        {
            var result = new RemoteSearchResult
            {
                Name = SelectName(anime, PluginConfiguration.Instance().TitlePreference, "en")
            };

            result.ImageUrl = anime.image_url_lge;
            result.SetProviderId(ProviderNames.AniList, anime.id.ToString());
            result.SearchProviderName = Name;

            DateTime start;
            if (DateTime.TryParse(anime.start_date, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out start))
                result.PremiereDate = start;

            return result;
        }

        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>();

            var aid = info.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (string.IsNullOrEmpty(aid))
                return result;

            if (!string.IsNullOrEmpty(aid))
            {
                result.Item = new Series();
                result.HasMetadata = true;

                result.Item.ProviderIds.Add(ProviderNames.AniList, aid);

                var anime = await _api.GetAnime(aid);

                result.Item.Name = SelectName(anime, PluginConfiguration.Instance().TitlePreference, info.MetadataLanguage ?? "en");
                result.Item.Overview = anime.description;

                DateTime start;
                if (DateTime.TryParse(anime.start_date, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out start))
                    result.Item.PremiereDate = start;

                DateTime end;
                if (DateTime.TryParse(anime.end_date, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out end))
                    result.Item.EndDate = end;

                if (anime.genres != null)
                {
                    foreach (var genre in anime.genres)
                        result.Item.AddGenre(genre);

                    if (result.Item.Genres == null || !result.Item.Genres.Contains("Animation"))
                        result.Item.AddGenre("Animation");
                }

                if (!string.IsNullOrEmpty(anime.image_url_lge))
                    StoreImageUrl(aid, anime.image_url_lge, "image");

                if (!string.IsNullOrEmpty(anime.image_url_banner))
                    StoreImageUrl(aid, anime.image_url_banner, "banner");
            }

            return result;
        }

        private string SelectName(Anime anime, TitlePreferenceType preference, string language)
        {
            if (preference == TitlePreferenceType.Localized && language == "en")
                return anime.title_english;

            if (preference == TitlePreferenceType.Japanese)
                return anime.title_japanese;

            return anime.title_romaji;
        }

        private void StoreImageUrl(string series, string url, string type)
        {
            var path = Path.Combine(_paths.CachePath, "anilist", type, series + ".txt");
            var directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);

            File.WriteAllText(path, url);
        }

        public static string GetSeriesImage(IApplicationPaths paths, string series, string type)
        {
            var path = Path.Combine(paths.CachePath, "anilist", type, series + ".txt");
            if (File.Exists(path))
                return File.ReadAllText(path);

            return null;
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    public class AniListSeriesImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClient _httpClient;
        private readonly IApplicationPaths _appPaths;

        public AniListSeriesImageProvider(IHttpClient httpClient, IApplicationPaths appPaths)
        {
            _httpClient = httpClient;
            _appPaths = appPaths;
        }

        public string Name => "AniList";

        public bool Supports(IHasImages item) => item is Series || item is Season;

        public IEnumerable<ImageType> GetSupportedImages(IHasImages item)
        {
            return new[] {ImageType.Primary, ImageType.Banner};
        }

        public Task<IEnumerable<RemoteImageInfo>> GetImages(IHasImages item, CancellationToken cancellationToken)
        {
            var seriesId = item.GetProviderId(ProviderNames.AniList);
            return GetImages(seriesId, cancellationToken);
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(string aid, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();

            if (!string.IsNullOrEmpty(aid))
            {
                var primary = AniListSeriesProvider.GetSeriesImage(_appPaths, aid, "image");
                if (!string.IsNullOrEmpty(primary))
                {
                    list.Add(new RemoteImageInfo
                    {
                        ProviderName = Name,
                        Type = ImageType.Primary,
                        Url = primary
                    });
                }

                var banner = AniListSeriesProvider.GetSeriesImage(_appPaths, aid, "banner");
                if (!string.IsNullOrEmpty(banner))
                {
                    list.Add(new RemoteImageInfo
                    {
                        ProviderName = Name,
                        Type = ImageType.Banner,
                        Url = banner
                    });
                }
            }

            return list;
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClient.GetResponse(new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = url,
                ResourcePool = AniDbSeriesProvider.ResourcePool

            });
        }
    }
}
