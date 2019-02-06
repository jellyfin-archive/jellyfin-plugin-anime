/*using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using MediaBrowser.Plugins.Anime.Providers.AniDB.Converter;

namespace MediaBrowser.Plugins.Anime.Providers.AniSearch
{
    /// <summary>
    ///     The <see cref="AniSearchEpisodeProvider" /> class provides episode metadata from AniSearch.
    /// </summary>
    public class AniSearchEpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>
    {
        private readonly IServerConfigurationManager _configurationManager;
        private readonly IHttpClient _httpClient;

        /// <summary>
        ///     Creates a new instance of the <see cref="AniDbEpisodeProvider" /> class.
        /// </summary>
        /// <param name="configurationManager">The configuration manager.</param>
        /// <param name="httpClient">The HTTP client.</param>
        public AniSearchEpisodeProvider(IServerConfigurationManager configurationManager, IHttpClient httpClient)
        {
            _configurationManager = configurationManager;
            _httpClient = httpClient;
        }

        public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Episode>();

            cancellationToken.ThrowIfCancellationRequested();

            var anisearchId = info.ProviderIds.GetOrDefault(ProviderNames.AniSearch);
            if (string.IsNullOrEmpty(anisearchId))
                return result;

            var id = AnidbEpisodeIdentity.Parse(anisearchId);
            if (id == null)
                return result;

            result.Item = new Episode
            {
                IndexNumber = info.IndexNumber,
                ParentIndexNumber = info.ParentIndexNumber
            };
            string url= "https://www.anisearch.de/anime/"+id+"/episodes";
            string web_content = await api.WebRequestAPI(url);
            result.HasMetadata = true;

            if (id.Value.EpisodeNumberEnd != null && id.Value.EpisodeNumberEnd > id.Value.EpisodeNumber)
            {
                for (var i = id.Value.EpisodeNumber + 1; i <= id.Value.EpisodeNumberEnd; i++)
                {
                    string episode = await api.One_line_regex(new System.Text.RegularExpressions.Regex("<span itemprop=\"name\" lang=\"de\" class=\"bold\">" + @"(.*?)<"), web_content, 1, i);
                    if(episode == "")
                    {
                    }
                    else
                    {
                    }
                }
            }

            return result;
        }

        public string Name => "AniSearch";

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            var list = new List<RemoteSearchResult>();

            var id = AnidbEpisodeIdentity.Parse(searchInfo.ProviderIds.GetOrDefault(ProviderNames.AniSearch));
            if (id == null)
            {
                //var episodeIdentifier = new AnidbEpisodeIdentityProvider();
                //await episodeIdentifier.Identify(searchInfo);

                //var converter = new AnidbTvdbEpisodeConverter();
                //await converter.Convert(searchInfo);

                //id = AnidbEpisodeIdentity.Parse(searchInfo.ProviderIds.GetOrDefault(ProviderNames.AniDb));
            }

            if (id == null)
                return list;

            try
            {
                var metadataResult = await GetMetadata(searchInfo, cancellationToken).ConfigureAwait(false);

                if (metadataResult.HasMetadata)
                {
                    var item = metadataResult.Item;

                    list.Add(new RemoteSearchResult
                    {
                        IndexNumber = item.IndexNumber,
                        Name = item.Name,
                        ParentIndexNumber = item.ParentIndexNumber,
                        PremiereDate = item.PremiereDate,
                        ProductionYear = item.ProductionYear,
                        ProviderIds = item.ProviderIds,
                        SearchProviderName = Name,
                        IndexNumberEnd = item.IndexNumberEnd
                    });
                }
            }
            catch (FileNotFoundException)
            {
                // Don't fail the provider because this will just keep on going and going.
            }
            catch (DirectoryNotFoundException)
            {
                // Don't fail the provider because this will just keep on going and going.
            }

            return list;
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
*/