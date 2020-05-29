using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Anime.Providers.AniDB.Metadata
{
    public class AniDbImageProvider : IRemoteImageProvider
    {
        public string Name => "AniDB";
        private readonly IHttpClient _httpClient;
        private readonly IApplicationPaths _appPaths;

        public AniDbImageProvider(IHttpClient httpClient, IApplicationPaths appPaths)
        {
            _httpClient = httpClient;
            _appPaths = appPaths;
        }

        public async Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            await AniDbSeriesProvider.RequestLimiter.Tick().ConfigureAwait(false);

            return await _httpClient.GetResponse(new HttpRequestOptions
            {
                UserAgent = Constants.UserAgent,
                CancellationToken = cancellationToken,
                Url = url
            }).ConfigureAwait(false);
        }

        public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var seriesId = item.GetProviderId(ProviderNames.AniDb);
            return GetImages(seriesId, cancellationToken);
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(string aniDbId, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();

            if (!string.IsNullOrEmpty(aniDbId))
            {
                var seriesDataPath = await AniDbSeriesProvider.GetSeriesData(_appPaths, _httpClient, aniDbId, cancellationToken);
                var imageUrl = await FindImageUrl(seriesDataPath).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    list.Add(new RemoteImageInfo
                    {
                        ProviderName = Name,
                        Url = imageUrl
                    });
                }
            }

            return list;
        }

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new[] { ImageType.Primary };
        }

        public bool Supports(BaseItem item)
        {
            return item is Series || item is Season || item is Movie;
        }

        private async Task<string> FindImageUrl(string seriesDataPath)
        {
            var settings = new XmlReaderSettings
            {
                Async = true,
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                ValidationType = ValidationType.None
            };

            using (var streamReader = new StreamReader(seriesDataPath, Encoding.UTF8))
            {
                using (XmlReader reader = XmlReader.Create(streamReader, settings))
                {
                    await reader.MoveToContentAsync().ConfigureAwait(false);

                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "picture")
                        {
                            return "https://cdn.anidb.net/images/main/" + reader.ReadElementContentAsString();
                        }
                    }
                }
            }

            return null;
        }
    }
}
