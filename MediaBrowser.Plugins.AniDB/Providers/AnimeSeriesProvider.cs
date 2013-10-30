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
using MediaBrowser.Plugins.AniDB.Providers.AniDB;

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
            {
                var people = SelectCollection(anidb.People, item.People.ToArray());
                item.People.Clear();
                foreach (var person in people)
                    item.AddPerson(person);
            }

            if (!item.LockedFields.Contains(MetadataFields.OfficialRating))
                item.OfficialRating = item.OfficialRating ?? anidb.ContentRating;

            if (!item.LockedFields.Contains(MetadataFields.Runtime))
                item.RunTimeTicks = anidb.RunTimeTicks ?? item.RunTimeTicks;

            if (!item.LockedFields.Contains(MetadataFields.Genres))
            {
                var genres = SelectCollection(item.Genres, anidb.Genres.ToArray());
                item.Genres.Clear();
                foreach (var genre in genres)
                    item.AddGenre(genre);
            }

            if (!item.LockedFields.Contains(MetadataFields.Studios))
            {
                var studios = SelectCollection(anidb.Studios, item.Studios.ToArray());
                item.Studios.Clear();
                foreach (var studio in studios)
                    item.AddStudio(studio);
            }

            item.PremiereDate = anidb.StartDate ?? item.PremiereDate;
            item.EndDate = anidb.EndDate ?? item.EndDate;
            item.Status = item.EndDate != null ? SeriesStatus.Ended : SeriesStatus.Continuing;
            item.AirTime = anidb.AirTime ?? item.AirTime;
            item.AirDays = SelectCollection(anidb.AirDays, item.AirDays).ToList();
            
            if (item.ProductionYear == null && item.PremiereDate != null)
            {
                item.ProductionYear = item.PremiereDate.Value.Year;
            }

            SeriesInfo mostVoted = (new[] {anidb}).OrderByDescending(info => info.VoteCount ?? 0).First();
            item.CommunityRating = mostVoted.CommunityRating;
            item.VoteCount = mostVoted.VoteCount;
        }

        private IEnumerable<T> SelectCollection<T>(params IEnumerable<T>[] items)
        {
            return items.FirstOrDefault(l => l != null && l.Any()) ?? new List<T>();
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
