using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugins.Anime.Providers.AniDB;

namespace MediaBrowser.Plugins.Anime.Providers
{
    public class AnimeSeasonProvider : BaseMetadataProvider
    {
        private readonly SeriesIndexSearch _indexSearch;
        private readonly AnimeSeriesProvider _seriesProvider;

        public AnimeSeasonProvider(ILogManager logManager, ILibraryManager library, IServerConfigurationManager configurationManager, IApplicationPaths appPaths, IHttpClient httpClient)
            : base(logManager, configurationManager)
        {
            _indexSearch = new SeriesIndexSearch(configurationManager, httpClient);
            _seriesProvider = new AnimeSeriesProvider(logManager, library, configurationManager, appPaths, httpClient);
        }
        
        public override MetadataProviderPriority Priority
        {
            get { return MetadataProviderPriority.First; }
        }

        protected override string ProviderVersion
        {
            get { return "2"; }
        }

        public override bool Supports(BaseItem item)
        {
            return item is Season;
        }

        public override async Task<bool> FetchAsync(BaseItem item, bool force, BaseProviderInfo providerInfo, CancellationToken cancellationToken)
        {
            if (!item.DontFetchMeta)
                await FetchSeriesData(item, cancellationToken);

            SetLastRefreshed(item, DateTime.Now, providerInfo);
            return true;
        }

        private async Task FetchSeriesData(BaseItem item, CancellationToken cancellationToken)
        {
            var season = (Season) item;
            string seriesId = season.Series.GetProviderId(ProviderNames.AniDb);

            if (!string.IsNullOrEmpty(seriesId))
            {
                if (season.IndexNumber == 1)
                {
                    var series = season.Series;

                    if (!season.LockedFields.Contains(MetadataFields.Name))
                        season.Name = series.Name;

                    if (!season.LockedFields.Contains(MetadataFields.Overview))
                        season.Overview = series.Overview;

                    if (!season.LockedFields.Contains(MetadataFields.Genres))
                        season.Genres = series.Genres;

                    if (!season.LockedFields.Contains(MetadataFields.Studios))
                        season.Studios = series.Studios;

                    if (!season.LockedFields.Contains(MetadataFields.Cast))
                        season.People = series.People;

                    if (!season.LockedFields.Contains(MetadataFields.OfficialRating))
                        season.OfficialRating = series.OfficialRating;

                    season.PremiereDate = series.PremiereDate;
                    season.EndDate = series.EndDate;
                    season.CommunityRating = series.CommunityRating;
                    season.VoteCount = series.VoteCount;

                    if (season.ProductionYear == null && season.PremiereDate != null)
                        season.ProductionYear = season.PremiereDate.Value.Year;
                }
                else
                {
                    seriesId = await _indexSearch.FindSeriesByRelativeIndex(seriesId, (season.IndexNumber ?? 1) - 1, cancellationToken).ConfigureAwait(false);
                    var seriesInfo = new SeriesInfo();
                    seriesInfo.ExternalProviders[ProviderNames.AniDb] = seriesId;
                    seriesInfo.Name = season.Name;

                    seriesInfo = await _seriesProvider.FindSeriesInfo(seriesInfo, item.GetPreferredMetadataLanguage(), cancellationToken).ConfigureAwait(false);

                    if (!season.LockedFields.Contains(MetadataFields.Name))
                        season.Name = seriesInfo.Name;

                    if (!season.LockedFields.Contains(MetadataFields.Overview))
                        season.Overview = seriesInfo.Description;

                    if (!season.LockedFields.Contains(MetadataFields.Genres))
                        season.Genres = seriesInfo.Genres;

                    if (!season.LockedFields.Contains(MetadataFields.Studios))
                        season.Studios = seriesInfo.Studios;

                    if (!season.LockedFields.Contains(MetadataFields.Cast))
                        season.People = seriesInfo.People;

                    if (!season.LockedFields.Contains(MetadataFields.OfficialRating))
                        season.OfficialRating = seriesInfo.ContentRating;

                    season.PremiereDate = seriesInfo.StartDate;
                    season.EndDate = seriesInfo.EndDate;
                    season.CommunityRating = seriesInfo.CommunityRating;
                    season.VoteCount = seriesInfo.VoteCount;

                    if (season.ProductionYear == null && season.PremiereDate != null)
                        season.ProductionYear = season.PremiereDate.Value.Year;
                }

                // calculate absolute season index (relative to original series)
                var seriesIndex = await _indexSearch.FindSeriesIndex(seriesId, cancellationToken).ConfigureAwait(false);
                season.IndexNumber += seriesIndex - 1;
            }
        }
    }
}