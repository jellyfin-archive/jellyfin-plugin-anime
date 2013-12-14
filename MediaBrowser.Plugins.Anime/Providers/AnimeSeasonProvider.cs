using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugins.Anime.Providers.AniDB;

namespace MediaBrowser.Plugins.Anime.Providers
{
    public class AnimeSeasonProvider : BaseMetadataProvider
    {
        private readonly SeriesIndexSearch _indexSearch;

        public AnimeSeasonProvider(ILogManager logManager, IServerConfigurationManager configurationManager, IHttpClient httpClient)
            : base(logManager, configurationManager)
        {
            _indexSearch = new SeriesIndexSearch(configurationManager, httpClient);
        }

        public override MetadataProviderPriority Priority
        {
            get { return MetadataProviderPriority.First; }
        }

        public override bool Supports(BaseItem item)
        {
            return item is Season;
        }

        public override async Task<bool> FetchAsync(BaseItem item, bool force, BaseProviderInfo providerInfo, CancellationToken cancellationToken)
        {
            var season = (Season) item;
            string seriesId = season.Series.GetProviderId(ProviderNames.AniDb);

            if (!string.IsNullOrEmpty(seriesId))
            {
                season.IndexNumber += await _indexSearch.FindSeriesIndex(seriesId, cancellationToken) - 1;
            }

            SetLastRefreshed(item, DateTime.Now, providerInfo);
            return true;
        }
    }
}