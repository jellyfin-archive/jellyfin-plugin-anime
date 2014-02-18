using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    public class AniDbSeriesImagesProvider : IRemoteImageProvider
    {
        private readonly IHttpClient _httpClient;
        private readonly IApplicationPaths _appPaths;

        public AniDbSeriesImagesProvider(IHttpClient httpClient, IApplicationPaths appPaths)
        {
            _httpClient = httpClient;
            _appPaths = appPaths;
        }

        public async Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            await AniDbSeriesProvider.RequestLimiter.Tick().ConfigureAwait(false);

            return await _httpClient.GetResponse(new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = url,
                ResourcePool = AniDbSeriesProvider.ResourcePool

            }).ConfigureAwait(false);
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(IHasImages item, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();

            var series = (Series)item;

            var seriesId = series.GetProviderId(ProviderNames.AniDb);

            if (!string.IsNullOrEmpty(seriesId) && series.IndexNumber.HasValue && series.IndexNumber.Value > 0)
            {
                var seriesDataDirectory = AniDbSeriesProvider.CalculateSeriesDataPath(_appPaths, seriesId);

                // TODO: Have an ensure method on AniDbSeriesProvider to download data if non-existant, or old based on some cache length (7d?)
                
                var seriesDataPath = Path.Combine(seriesDataDirectory, "series.xml");
                var imageUrl = FindImageUrl(seriesDataPath);

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
            return item is Series;
        }

        private string FindImageUrl(string seriesDataPath)
        {
            var settings = new XmlReaderSettings
            {
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                ValidationType = ValidationType.None
            };

            using (var streamReader = new StreamReader(seriesDataPath, Encoding.UTF8))
            {
                using (XmlReader reader = XmlReader.Create(streamReader, settings))
                {
                    reader.MoveToContent();

                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "picture")
                        {
                            return "http://img7.anidb.net/pics/anime/" + reader.ReadElementContentAsString();
                        }
                    }
                }
            }

            return null;
        }
    }
}