using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    public class AnimeEpisodeProvider : BaseMetadataProvider
    {
        private readonly IEnumerable<IEpisodeProvider> _allProviders;
        private readonly AniDbEpisodeProvider _aniDb;

        public AnimeEpisodeProvider(ILogManager logManager, IServerConfigurationManager configurationManager, IHttpClient httpClient) : base(logManager, configurationManager)
        {
            _aniDb = new AniDbEpisodeProvider(configurationManager, httpClient);

            _allProviders = new[]
            {
                _aniDb
            };
        }

        public override MetadataProviderPriority Priority
        {
            get { return MetadataProviderPriority.Fourth; }
        }

        public override ItemUpdateType ItemUpdateType
        {
            get { return ItemUpdateType.MetadataImport; }
        }

        public override bool RequiresInternet
        {
            get { return _allProviders.Any(p => p.RequiresInternet); }
        }

        protected override bool RefreshOnVersionChange
        {
            get { return true; }
        }

        protected override string ProviderVersion
        {
            get { return "1"; }
        }

        public override bool Supports(BaseItem item)
        {
            return item is Episode;
        }

        public override async Task<bool> FetchAsync(BaseItem item, bool force, BaseProviderInfo providerInfo, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var episode = (Episode) item;

            // get anidb info
            EpisodeInfo anidb = await _aniDb.FindEpisodeInfo(episode, cancellationToken);
            AddProviders(episode, anidb.ExternalProviders);

            // merge results
            MergeEpisodeInfo(episode, anidb);

            SetLastRefreshed(item, DateTime.UtcNow, providerInfo);
            return true;
        }

        protected override bool NeedsRefreshBasedOnCompareDate(BaseItem item, BaseProviderInfo providerInfo)
        {
            if (_allProviders.Any(p => p.NeedsRefreshBasedOnCompareDate(item, providerInfo)))
                return true;

            return base.NeedsRefreshBasedOnCompareDate(item, providerInfo);
        }

        private void MergeEpisodeInfo(Episode item, EpisodeInfo anidb)
        {
            if (!item.LockedFields.Contains(MetadataFields.Name))
                item.Name = anidb.Name ?? item.Name;

            if (!item.LockedFields.Contains(MetadataFields.Overview))
                item.Overview = anidb.Description ?? item.Overview;

            if (!item.LockedFields.Contains(MetadataFields.Cast))
            {
                IEnumerable<PersonInfo> people = SelectCollection(anidb.People, item.People.ToArray());
                item.People.Clear();
                foreach (PersonInfo person in people)
                    item.AddPerson(person);
            }

            if (!item.LockedFields.Contains(MetadataFields.Runtime))
                item.RunTimeTicks = anidb.RunTimeTicks ?? item.RunTimeTicks;

            if (!item.LockedFields.Contains(MetadataFields.Genres))
            {
                IEnumerable<string> genres = SelectCollection(item.Genres, anidb.Genres.ToArray());
                item.Genres.Clear();
                foreach (string genre in genres)
                    item.AddGenre(genre);
            }

            if (!item.LockedFields.Contains(MetadataFields.Studios))
            {
                IEnumerable<string> studios = SelectCollection(anidb.Studios, item.Studios.ToArray());
                item.Studios.Clear();
                foreach (string studio in studios)
                    item.AddStudio(studio);
            }

            item.PremiereDate = anidb.AirDate ?? item.PremiereDate;

            if (item.ProductionYear == null && item.PremiereDate != null)
            {
                item.ProductionYear = item.PremiereDate.Value.Year;
            }

            EpisodeInfo mostVoted = (new[] {anidb}).OrderByDescending(info => info.VoteCount ?? 0).First();
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