using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugins.Anime.Configuration;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    public class AniDbSeriesImagesProvider : BaseMetadataProvider
    {
        private readonly IProviderManager _providerManager;
        private readonly SeriesIndexSearch _indexSearch;

        public AniDbSeriesImagesProvider(ILogManager logManager, IServerConfigurationManager configurationManager, IProviderManager providerManager, IHttpClient httpClient)
            : base(logManager, configurationManager)
        {
            _providerManager = providerManager;
            _indexSearch = new SeriesIndexSearch(configurationManager, httpClient);
        }

        public override MetadataProviderPriority Priority
        {
            get { return MetadataProviderPriority.Last; }
        }

        public override bool RequiresInternet
        {
            get { return true; }
        }

        public override ItemUpdateType ItemUpdateType
        {
            get { return ItemUpdateType.ImageUpdate; }
        }

        protected override bool RefreshOnVersionChange
        {
            get { return true; }
        }

        protected override string ProviderVersion
        {
            get { return "1"; }
        }

        public override bool Supports(BaseItem item)
        {
            return item is Series;
        }

        protected override DateTime CompareDate(BaseItem item)
        {
            string seriesId = item.GetProviderId(ProviderNames.AniDb);

            if (!string.IsNullOrEmpty(seriesId))
            {
                string imagesXmlPath = Path.Combine(AniDbSeriesProvider.CalculateSeriesDataPath(ConfigurationManager.ApplicationPaths, seriesId), "series.xml");
                var imagesFileInfo = new FileInfo(imagesXmlPath);

                if (imagesFileInfo.Exists)
                {
                    return imagesFileInfo.LastWriteTimeUtc;
                }
            }

            return base.CompareDate(item);
        }

        protected override bool NeedsRefreshInternal(BaseItem item, BaseProviderInfo providerInfo)
        {
            var id = item.GetProviderId(ProviderNames.AniDb);
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            if (item.HasImage(ImageType.Primary) && !item.LockedFields.Contains(MetadataFields.Images))
            {
                var seriesIndex = _indexSearch.FindSeriesIndex(id, CancellationToken.None).Result;
                return seriesIndex != 1;
            }

            return base.NeedsRefreshInternal(item, providerInfo);
        }

        public override async Task<bool> FetchAsync(BaseItem item, bool force, BaseProviderInfo providerInfo, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var series = (Series) item;
            string seriesId = series.GetProviderId(ProviderNames.AniDb);
            
            if (!string.IsNullOrEmpty(seriesId) && !item.LockedFields.Contains(MetadataFields.Images) && (!item.HasImage(ImageType.Primary) || await ShouldOverrideImage(seriesId)))
            {
                string seriesDataDirectory = AniDbSeriesProvider.CalculateSeriesDataPath(ConfigurationManager.ApplicationPaths, seriesId);
                string seriesDataPath = Path.Combine(seriesDataDirectory, "series.xml");
                string imageUrl = FindImageUrl(seriesDataPath);

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    Logger.Debug("Downloading primary image for {0} from {1}", item.Name, imageUrl);

                    await AniDbSeriesProvider.RequestLimiter.Tick();
                    await _providerManager.SaveImage(series, imageUrl, AniDbSeriesProvider.ResourcePool, ImageType.Primary, null, cancellationToken)
                                          .ConfigureAwait(false);

                    SetLastRefreshed(item, DateTime.UtcNow, providerInfo);
                    return true;
                }
            }

            return false;
        }

        private Task<bool> ShouldOverrideImage(string seriesId)
        {
            return TvdbImageIsLikelyWrong(seriesId);
        }

        private async Task<bool> TvdbImageIsLikelyWrong(string seriesId)
        {
            var seriesIndex = await _indexSearch.FindSeriesIndex(seriesId, CancellationToken.None);
            return seriesIndex != 0;
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