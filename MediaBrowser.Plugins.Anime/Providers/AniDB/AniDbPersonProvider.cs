using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    public class AniDbPersonProvider : BaseMetadataProvider
    {
        private readonly ILibraryManager _library;

        public AniDbPersonProvider(ILogManager logManager, IServerConfigurationManager configurationManager, ILibraryManager library)
            : base(logManager, configurationManager)
        {
            _library = library;
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
            get { return false; }
        }

        public override MetadataProviderPriority Priority
        {
            get { return MetadataProviderPriority.Fourth; }
        }

        public override ItemUpdateType ItemUpdateType
        {
            get { return ItemUpdateType.MetadataImport; }
        }

        public override bool Supports(BaseItem item)
        {
            return item is Person;
        }

        public override Task<bool> FetchAsync(BaseItem item, bool force, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(item.GetProviderId(ProviderNames.AniDb)))
                return Task.FromResult(false);
            
            List<Series> seriesWithPerson = _library.RootFolder
                                                    .RecursiveChildren
                                                    .OfType<Series>()
                                                    .Where(i => !string.IsNullOrEmpty(i.GetProviderId(ProviderNames.AniDb)) && i.People.Any(p => string.Equals(p.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
                                                    .ToList();

            foreach (Series series in seriesWithPerson)
            {
                try
                {
                    string seriesPath = AniDbSeriesProvider.CalculateSeriesDataPath(ConfigurationManager.ApplicationPaths, series.GetProviderId(ProviderNames.AniDb));
                    AniDbPersonInfo person = TryFindPerson(item.Name, seriesPath);
                    if (person != null)
                    {
                        if (!string.IsNullOrEmpty(person.Id))
                        {
                            item.SetProviderId(ProviderNames.AniDb, person.Id);
                        }

                        break;
                    }
                }
                catch (FileNotFoundException)
                {
                    // No biggie
                }
            }

            SetLastRefreshed(item, DateTime.UtcNow);
            return Task.FromResult(true);
        }

        public static AniDbPersonInfo TryFindPerson(string name, string dataPath)
        {
            string castXml = Path.Combine(dataPath, "cast.xml");

            if (string.IsNullOrEmpty(castXml) || !File.Exists(castXml))
            {
                return null;
            }

            var serializer = new XmlSerializer(typeof (CastList));
            using (FileStream stream = File.Open(castXml, FileMode.Open, FileAccess.Read))
            {
                var list = (CastList) serializer.Deserialize(stream);
                return list.Cast.FirstOrDefault(p => string.Equals(name, AniDbSeriesProvider.ReverseNameOrder(p.Name), StringComparison.OrdinalIgnoreCase));
            }
        }
    }

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

        public override MetadataProviderPriority Priority
        {
            get { return MetadataProviderPriority.Fourth; }
        }

        public override ItemUpdateType ItemUpdateType
        {
            get { return ItemUpdateType.MetadataImport | ItemUpdateType.ImageUpdate; }
        }

        public override bool Supports(BaseItem item)
        {
            return item is Person;
        }

        public override async Task<bool> FetchAsync(BaseItem item, bool force, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(item.PrimaryImagePath))
            {
                List<Series> seriesWithPerson = _library.RootFolder
                                                        .RecursiveChildren
                                                        .OfType<Series>()
                                                        .Where(i => !string.IsNullOrEmpty(i.GetProviderId(ProviderNames.AniDb)) && i.People.Any(p => string.Equals(p.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
                                                        .ToList();

                foreach (Series series in seriesWithPerson)
                {
                    try
                    {
                        string seriesPath = AniDbSeriesProvider.CalculateSeriesDataPath(ConfigurationManager.ApplicationPaths, series.GetProviderId(ProviderNames.AniDb));
                        AniDbPersonInfo person = AniDbPersonProvider.TryFindPerson(item.Name, seriesPath);
                        if (person != null)
                        {
                            if (!string.IsNullOrEmpty(person.Id))
                            {
                                item.SetProviderId(ProviderNames.AniDb, person.Id);
                            }

                            if (!string.IsNullOrEmpty(person.Image))
                            {
                                await _providerManager.SaveImage(item, person.Image, AniDbSeriesProvider.ResourcePool, ImageType.Primary, null, cancellationToken)
                                                      .ConfigureAwait(false);
                            }

                            break;
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        // No biggie
                    }
                }
            }

            SetLastRefreshed(item, DateTime.UtcNow);
            return true;
        }
    }
}