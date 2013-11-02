using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    public class AniDbSeriesImagesProvider : BaseMetadataProvider
    {
        private readonly IProviderManager _providerManager;

        public AniDbSeriesImagesProvider(ILogManager logManager, IServerConfigurationManager configurationManager, IProviderManager providerManager) 
            : base(logManager, configurationManager)
        {
            _providerManager = providerManager;
        }

        public override MetadataProviderPriority Priority
        {
            get { return MetadataProviderPriority.Fifth; }
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
                string imagesXmlPath = Path.Combine(AniDbSeriesProvider.GetSeriesDataPath(ConfigurationManager.ApplicationPaths, seriesId), "series.xml");
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
            if (item.HasImage(ImageType.Primary))
            {
                return false;
            }

            return base.NeedsRefreshInternal(item, providerInfo);
        }

        public override async Task<bool> FetchAsync(BaseItem item, bool force, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var series = (Series) item;
            string seriesId = series.GetProviderId(ProviderNames.AniDb);

            if (!string.IsNullOrEmpty(seriesId) && !item.HasImage(ImageType.Primary))
            {
                string seriesDataDirectory = AniDbSeriesProvider.GetSeriesDataPath(ConfigurationManager.ApplicationPaths, seriesId);
                string seriesDataPath = Path.Combine(seriesDataDirectory, "series.xml");
                string imageUrl = FindImageUrl(seriesDataPath);

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    Logger.Debug("Downloading primary image for {0} from {1}", item.Name, imageUrl);

                    await _providerManager.SaveImage(series, imageUrl, AniDbSeriesProvider.ResourcePool, ImageType.Primary, null, cancellationToken)
                                          .ConfigureAwait(false);

                    SetLastRefreshed(item, DateTime.UtcNow);
                    return true;
                }
            }

            return false;
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