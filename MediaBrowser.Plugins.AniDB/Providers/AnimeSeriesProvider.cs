using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;

namespace MediaBrowser.Plugins.AniDB.Providers
{
    public class AnimeSeriesProvider : BaseMetadataProvider
    {
        private readonly AniDbSeriesProvider _aniDbProvider;

        public AnimeSeriesProvider(ILogManager logManager, IServerConfigurationManager configurationManager, IApplicationPaths appPaths, IHttpClient httpClient) 
            : base(logManager, configurationManager)
        {
            _aniDbProvider = new AniDbSeriesProvider(logManager.GetLogger("AniDB"), configurationManager, appPaths, httpClient);
        }

        public override bool EnforceDontFetchMetadata
        {
            get { return false; }
        }

        public override MetadataProviderPriority Priority
        {
            get { return MetadataProviderPriority.Third; }
        }

        public override bool RequiresInternet
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

        protected override bool NeedsRefreshBasedOnCompareDate(BaseItem item, BaseProviderInfo providerInfo)
        {
            if (_aniDbProvider.NeedsRefreshBasedOnCompareDate(item, providerInfo))
                return true;

            return base.NeedsRefreshBasedOnCompareDate(item, providerInfo);
        }

        public override async Task<bool> FetchAsync(BaseItem item, bool force, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var series = (Series)item;

            // get anidb info
            var anidb = await _aniDbProvider.FindSeriesInfo(series, cancellationToken);
            AddProviders(series, anidb.ExternalProviders);

            // get mal/anilist info

            if (!series.DontFetchMeta)
            {
                MergeSeriesInfo(series, anidb);
            }

            SetLastRefreshed(item, DateTime.UtcNow);
            return true;
        }

        private void MergeSeriesInfo(Series item, SeriesInfo anidb)
        {
            if (!item.LockedFields.Contains(MetadataFields.Name))
                item.Name = anidb.Name ?? item.Name;

            if (!item.LockedFields.Contains(MetadataFields.Overview))
                item.Overview = item.Overview ?? anidb.Description;

            if (!item.LockedFields.Contains(MetadataFields.Cast))
                item.People = SelectCollection(anidb.People, item.People);

            if (!item.LockedFields.Contains(MetadataFields.OfficialRating))
                item.OfficialRating = item.OfficialRating ?? anidb.ContentRating;

            if (!item.LockedFields.Contains(MetadataFields.Runtime))
                item.RunTimeTicks = anidb.RunTimeTicks ?? item.RunTimeTicks;

            if (!item.LockedFields.Contains(MetadataFields.Genres))
                item.Genres = SelectCollection(item.Genres, anidb.Genres);

            if (!item.LockedFields.Contains(MetadataFields.Studios))
                item.Studios = SelectCollection(anidb.Studios, item.Studios);

            item.PremiereDate = anidb.StartDate ?? item.PremiereDate;
            item.EndDate = anidb.EndDate ?? item.EndDate;
            item.Status = item.EndDate != null ? SeriesStatus.Ended : SeriesStatus.Continuing;
            item.AirTime = anidb.AirTime ?? item.AirTime;
            item.AirDays = SelectCollection(anidb.AirDays, item.AirDays);

            if (item.ProductionYear == null && item.PremiereDate != null)
            {
                item.ProductionYear = item.PremiereDate.Value.Year;
            }

            SeriesInfo mostVoted = (new[] {anidb}).OrderByDescending(info => info.VoteCount ?? 0).First();
            item.CommunityRating = mostVoted.CommunityRating;
            item.VoteCount = mostVoted.VoteCount;
        }

        private List<T> SelectCollection<T>(params List<T>[] items)
        {
            return items.FirstOrDefault(l => l != null && l.Count > 0) ?? new List<T>();
        }

        private void AddProviders(BaseItem item, Dictionary<string, string> providers)
        {
            foreach (var provider in providers)
            {
                item.ProviderIds[provider.Key] = provider.Value;
            }
        }
    }
}
