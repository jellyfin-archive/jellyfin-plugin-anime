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
    public class EpisodeParentIndexUpdater
        : BaseMetadataProvider
    {
        private readonly SeriesIndexSearch _indexSearch;

        public EpisodeParentIndexUpdater(ILogManager logManager, IServerConfigurationManager configurationManager, IHttpClient httpClient)
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
            return item is Episode;
        }

        public override async Task<bool> FetchAsync(BaseItem item, bool force, BaseProviderInfo providerInfo, CancellationToken cancellationToken)
        {
            var episode = (Episode) item;

            var season = episode.Parent as Season;
            if (season != null)
            {
                // update the parent index if the parent is a season, to account for any adjustments made
                // by the AnimeSeriesProvider
                if (season.IndexNumber != null)
                    episode.ParentIndexNumber = season.IndexNumber;
            }
            else
            {
                var series = episode.Parent as Series;
                if (series != null)
                {
                    string seriesId = series.GetProviderId(ProviderNames.AniDb);

                    if (!string.IsNullOrEmpty(seriesId))
                        episode.ParentIndexNumber += await _indexSearch.FindSeriesIndex(seriesId, cancellationToken) - 1;
                }
            }

            SetLastRefreshed(item, DateTime.Now, providerInfo);
            return true;
        }
    }
}