using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Anime.Providers.AniDB.Metadata
{
    public class AniDbPersonProvider : IRemoteMetadataProvider<Person, PersonLookupInfo>
    {
        private readonly IApplicationPaths _paths;

        public AniDbPersonProvider(IApplicationPaths paths)
        {
            _paths = paths;
        }

        public Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Person>();

            if (!string.IsNullOrEmpty(info.ProviderIds.GetOrDefault(ProviderNames.AniDb)))
            {
                return Task.FromResult(result);
            }

            var person = AniDbSeriesProvider.GetPersonInfo(_paths.CachePath, info.Name);
            if (!string.IsNullOrEmpty(person?.Id))
            {
                result.Item = new Person();
                result.HasMetadata = true;

                result.Item.SetProviderId(ProviderNames.AniDb, person.Id);
            }

            return Task.FromResult(result);
        }

        public string Name => "AniDB";

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo searchInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
        }

        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    public class AniDbPersonImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IApplicationPaths _paths;

        public AniDbPersonImageProvider(IApplicationPaths paths, IHttpClientFactory httpClientFactory)
        {
            _paths = paths;
            _httpClientFactory = httpClientFactory;
        }

        public bool Supports(BaseItem item)
        {
            return item is Person;
        }

        public string Name => "AniDB";

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            yield return ImageType.Primary;
        }

        public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var infos = new List<RemoteImageInfo>();

            var person = AniDbSeriesProvider.GetPersonInfo(_paths.CachePath, item.Name);
            if (person != null)
            {
                infos.Add(new RemoteImageInfo
                {
                    Url = person.Image,
                    Type = ImageType.Primary,
                    ProviderName = Name
                });
            }

            return Task.FromResult<IEnumerable<RemoteImageInfo>>(infos);
        }

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            await AniDbSeriesProvider.RequestLimiter.Tick().ConfigureAwait(false);
            var httpClient = _httpClientFactory.CreateClient(Plugin.Instance.Id.ToString());

            return await httpClient.GetAsync(url).ConfigureAwait(false);
        }
    }
}
