using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;

namespace MediaBrowser.Plugins.AniDB.Providers.AniDB
{
    public class AniDbPersonImageProvider : BaseMetadataProvider
    {
        private readonly ILibraryManager _library;
        private readonly IProviderManager _providerManager;

        public AniDbPersonImageProvider(ILogManager logManager, IServerConfigurationManager configurationManager, ILibraryManager library, IProviderManager providerManager)
            : base(logManager, configurationManager)
        {
            _library = library;
            _providerManager = providerManager;
        }

        protected override bool RefreshOnVersionChange
        {
            get { return true; }
        }

        protected override string ProviderVersion
        {
            get { return "1"; }
        }

        public override bool RequiresInternet
        {
            get { return true; }
        }

        // lie for the moment
        public override bool IsSlow
        {
            get { return false; }
        }

        public override bool Supports(BaseItem item)
        {
            return item is Person;
        }

        public override MetadataProviderPriority Priority
        {
            get { return MetadataProviderPriority.Fourth; }
        }

        public override ItemUpdateType ItemUpdateType
        {
            get { return ItemUpdateType.ImageUpdate; }
        }

        public override async Task<bool> FetchAsync(BaseItem item, bool force, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(item.PrimaryImagePath))
            {
                var seriesWithPerson = _library.RootFolder
                    .RecursiveChildren
                    .OfType<Series>()
                    .Where(i => !string.IsNullOrEmpty(i.GetProviderId(ProviderNames.AniDb)) && i.People.Any(p => string.Equals(p.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                foreach (var series in seriesWithPerson)
                {
                    try
                    {
                        await DownloadImageFromSeries(item, series, cancellationToken).ConfigureAwait(false);
                    }
                    catch (FileNotFoundException)
                    {
                        // No biggie
                        continue;
                    }

                    // break once we have an image
                    if (!string.IsNullOrEmpty(item.PrimaryImagePath))
                    {
                        break;
                    }
                }

            }

            SetLastRefreshed(item, DateTime.UtcNow);
            return true;
        }

        private async Task DownloadImageFromSeries(BaseItem item, Series series, CancellationToken cancellationToken)
        {
            var dataPath = AniDbSeriesProvider.GetSeriesDataPath(ConfigurationManager.ApplicationPaths, series.GetProviderId(ProviderNames.AniDb));
            var castXml = Path.Combine(dataPath, "cast.xml");

            if (string.IsNullOrEmpty(castXml) || !File.Exists(castXml))
            {
                return;
            }

            var url = FetchImageUrl(item, castXml);

            if (!string.IsNullOrEmpty(url))
            {
                await _providerManager.SaveImage(item, url, AniDbSeriesProvider.ResourcePool, ImageType.Primary, null, cancellationToken)
                                      .ConfigureAwait(false);
            }
        }

        private string FetchImageUrl(BaseItem item, string castXml)
        {
            var doc = XDocument.Load(castXml);

            var characters = doc.Element("characters");
            if (characters != null)
            {
                foreach (var character in characters.Descendants("character"))
                {
                    var seiyuu = character.Element("seiyuu");
                    if (seiyuu != null && string.Equals(seiyuu.Value, AniDbSeriesProvider.ReverseNameOrder(item.Name), StringComparison.OrdinalIgnoreCase))
                    {
                        var picture = seiyuu.Attribute("picture");
                        if (picture != null && !string.IsNullOrEmpty(picture.Value))
                        {
                            return "http://img7.anidb.net/pics/anime/" + picture.Value;
                        }
                    }
                }
            }

            return null;
        }
    }
}
