using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    public class AniDbPersonProvider : IRemoteMetadataProvider<Person, PersonLookupInfo>
    {
        private readonly IServerConfigurationManager _configurationManager;
        private readonly ILibraryManager _library;

        public AniDbPersonProvider(IServerConfigurationManager configurationManager, ILibraryManager library)
        {
            _configurationManager = configurationManager;
            _library = library;
        }

        public async Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Person>();

            if (!string.IsNullOrEmpty(info.ProviderIds.GetOrDefault(ProviderNames.AniDb)))
                return result;

            List<Series> seriesWithPerson = _library.RootFolder
                                                    .RecursiveChildren
                                                    .OfType<Series>()
                                                    .Where(i => !string.IsNullOrEmpty(i.GetProviderId(ProviderNames.AniDb)) && i.People.Any(p => string.Equals(p.Name, info.Name, StringComparison.OrdinalIgnoreCase)))
                                                    .ToList();

            foreach (Series series in seriesWithPerson)
            {
                try
                {
                    string seriesPath = AniDbSeriesProvider.CalculateSeriesDataPath(_configurationManager.ApplicationPaths, series.GetProviderId(ProviderNames.AniDb));
                    AniDbPersonInfo person = TryFindPerson(info.Name, seriesPath);
                    if (person != null)
                    {
                        if (!string.IsNullOrEmpty(person.Id))
                        {
                            result.Item = new Person();
                            result.HasMetadata = true;

                            result.Item.SetProviderId(ProviderNames.AniDb, person.Id);
                        }

                        break;
                    }
                }
                catch (FileNotFoundException)
                {
                    // No biggie
                }
            }

            return result;
        }

        public string Name
        {
            get { return "AniDB"; }
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo searchInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
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

    public class AniDbPersonImageProvider : IRemoteImageProvider
    {
        private readonly IServerConfigurationManager _configurationManager;
        private readonly IHttpClient _httpClient;
        private readonly ILibraryManager _library;
        private readonly IProviderManager _providerManager;

        public AniDbPersonImageProvider(IServerConfigurationManager configurationManager, ILibraryManager library, IProviderManager providerManager, IHttpClient httpClient)
        {
            _configurationManager = configurationManager;
            _library = library;
            _providerManager = providerManager;
            _httpClient = httpClient;
        }

        public bool Supports(IHasImages item)
        {
            return item is Person;
        }

        public string Name
        {
            get { return "AniDB"; }
        }

        public IEnumerable<ImageType> GetSupportedImages(IHasImages item)
        {
            yield return ImageType.Primary;
        }

        public Task<IEnumerable<RemoteImageInfo>> GetImages(IHasImages item, CancellationToken cancellationToken)
        {
            List<Series> seriesWithPerson = _library.RootFolder
                                                    .RecursiveChildren
                                                    .OfType<Series>()
                                                    .Where(i => !string.IsNullOrEmpty(i.GetProviderId(ProviderNames.AniDb)) && i.People.Any(p => string.Equals(p.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
                                                    .ToList();

            IEnumerable<RemoteImageInfo> infos = seriesWithPerson.Select(i => GetImageFromSeriesData(i, item.Name))
                                                                 .Where(i => i != null)
                                                                 .Take(1);

            return Task.FromResult(infos);
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

        private RemoteImageInfo GetImageFromSeriesData(Series series, string personName)
        {
            string seriesPath = AniDbSeriesProvider.CalculateSeriesDataPath(_configurationManager.ApplicationPaths, series.GetProviderId(ProviderNames.AniDb));
            AniDbPersonInfo person = AniDbPersonProvider.TryFindPerson(personName, seriesPath);
            if (person != null)
            {
                return new RemoteImageInfo
                {
                    Url = person.Image,
                    Type = ImageType.Primary,
                    ProviderName = Name
                };
            }

            return null;
        }
    }
}