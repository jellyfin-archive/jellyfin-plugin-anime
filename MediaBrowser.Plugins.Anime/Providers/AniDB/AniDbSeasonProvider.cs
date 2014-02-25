using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    public class AniDbSeasonProvider : IRemoteMetadataProvider<Season, SeasonInfo>
    {
        public async Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Season>();
            return result;

            // todo copy series data into seasons
            // string seriesId = season.Series.GetProviderId(ProviderNames.AniDb);
            // string seasonid = await _indexSearch.FindSeriesByRelativeIndex(seriesId, (season.IndexNumber ?? 1) - 1, cancellationToken).ConfigureAwait(false);
        }

        public string Name
        {
            get { return "AniDB"; }
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}