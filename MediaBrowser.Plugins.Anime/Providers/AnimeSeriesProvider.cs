using System;
using System.Collections.Generic;
using System.Linq;
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
using MediaBrowser.Plugins.Anime.Configuration;
using MediaBrowser.Plugins.Anime.Providers.AniDB;
using MediaBrowser.Plugins.Anime.Providers.AniList;
using MediaBrowser.Plugins.Anime.Providers.MyAnimeList;

namespace MediaBrowser.Plugins.Anime.Providers
{
    public class AnimeSeriesProvider : BaseMetadataProvider
    {
        private const int MaxGenres = 4;

        private readonly ILibraryManager _library;
        private readonly IEnumerable<ISeriesProvider> _allProviders;
        private readonly AniDbSeriesProvider _aniDbProvider;
        private readonly AniListSeriesProvider _aniListProvider;
        private readonly MalSeriesProvider _malProvider;
        
        public AnimeSeriesProvider(ILogManager logManager, ILibraryManager library, IServerConfigurationManager configurationManager, IApplicationPaths appPaths, IHttpClient httpClient)
            : base(logManager, configurationManager)
        {
            _library = library;
            _aniDbProvider = new AniDbSeriesProvider(logManager.GetLogger("AniDB"), appPaths, httpClient);
            _malProvider = new MalSeriesProvider(new MalSeriesDownloader(appPaths, logManager.GetLogger("MalDownloader")), logManager.GetLogger("MyAnimeList"));
            _aniListProvider = new AniListSeriesProvider(new AniListSeriesDownloader(appPaths, logManager.GetLogger("AniListDownloader")), logManager.GetLogger("AniList"));

            _allProviders = new ISeriesProvider[]
            {
                _aniDbProvider,
                _aniListProvider,
                _malProvider
            };
        }

        public override bool EnforceDontFetchMetadata
        {
            get { return false; }
        }

        public override MetadataProviderPriority Priority
        {
            get
            {
                // run after tvdb and imdb
                return (MetadataProviderPriority)6;
            }
        }

        public override bool RequiresInternet
        {
            get { return _allProviders.Any(p => p.RequiresInternet); }
        }

        protected override string ProviderVersion
        {
            get { return "4"; }
        }

        public override bool Supports(BaseItem item)
        {
            return item is Series;
        }

        protected override bool NeedsRefreshBasedOnCompareDate(BaseItem item, BaseProviderInfo providerInfo)
        {
            if (_allProviders.Any(p => p.NeedsRefreshBasedOnCompareDate(item, providerInfo)))
                return true;

            return base.NeedsRefreshBasedOnCompareDate(item, providerInfo);
        }

        public override async Task<bool> FetchAsync(BaseItem item, bool force, BaseProviderInfo providerInfo, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var series = (Series) item;
            
            // ignore series we can be fairly certain are not anime, or that the user has marked as ignored
            if (SeriesNotAnimated(series) || SeriesIsIgnored(series))
            {
                RemoveProviderIds(series, ProviderNames.AniDb, ProviderNames.AniList, ProviderNames.MyAnimeList);
                return false;
            }

            // get anidb info
            SeriesInfo anidb = await _aniDbProvider.FindSeriesInfo(series, item.GetPreferredMetadataLanguage(), cancellationToken);
            AddProviders(series, anidb.ExternalProviders);

            // get anilist info
            SeriesInfo anilist = await _aniListProvider.FindSeriesInfo(series, item.GetPreferredMetadataLanguage(), cancellationToken);
            AddProviders(series, anilist.ExternalProviders);

            // get mal info
            SeriesInfo mal = await _malProvider.FindSeriesInfo(series, item.GetPreferredMetadataLanguage(), cancellationToken);
            AddProviders(series, mal.ExternalProviders);
            
            if (!series.DontFetchMeta)
            {
                if (force || PluginConfiguration.Instance().AllowAutomaticMetadataUpdates || !item.ResolveArgs.ContainsMetaFileByName("series.xml"))
                {
                    MergeSeriesInfo(series, anidb, anilist, mal);
                }
            }

            SetLastRefreshed(item, DateTime.UtcNow, providerInfo);
            return true;
        }
        
        private void RemoveProviderIds(BaseItem item, params string[] ids)
        {
            foreach (var id in ids)
            {
                if (!string.IsNullOrEmpty(item.GetProviderId(id)))
                    item.SetProviderId(id, null);
            }
        }

        private bool SeriesNotAnimated(Series series)
        {
            var recognised = !string.IsNullOrEmpty(series.GetProviderId(MetadataProviders.Tvdb)) ||
                             !string.IsNullOrEmpty(series.GetProviderId(MetadataProviders.Imdb));

            var isEnglishMetadata = string.Equals(series.GetPreferredMetadataLanguage(), "en", StringComparison.OrdinalIgnoreCase);

            return recognised && isEnglishMetadata && !series.Genres.Contains("Animation");
        }

        private bool SeriesIsIgnored(Series series)
        {
            var config = PluginConfiguration.Instance();

            var ignoredVirtualFolderLocations = _library.GetDefaultVirtualFolders().Where(vf => config.IgnoredVirtualFolders.Contains(vf.Name))
                                                        .SelectMany(vf => vf.Locations)
                                                        .ToList();

            for (var item = series.Parent; item != null; item = item.Parent)
            {
                if (!item.IsFolder)
                    continue;
                
                if (config.IgnoredPhysicalLocations.Contains(item.Path))
                {
                    return true;
                }

                if (ignoredVirtualFolderLocations.Contains(item.Path))
                {
                    return true;
                }
            }

            return false;
        }

        private void MergeSeriesInfo(Series item, SeriesInfo anidb, SeriesInfo anilist, SeriesInfo mal)
        {
            if (!item.LockedFields.Contains(MetadataFields.Name))
                item.Name = anidb.Name ?? anilist.Name ?? mal.Name ?? item.Name;

            // prefer existing (tvdb) overview, as it is localized
            if (!item.LockedFields.Contains(MetadataFields.Overview))
                item.Overview = item.Overview ?? anilist.Description ?? mal.Description ?? anidb.Description;
            
            if (!item.LockedFields.Contains(MetadataFields.Cast))
            {
                IEnumerable<PersonInfo> people = SelectCollection(anidb.People, anilist.People, mal.People, item.People.ToArray());
                item.People.Clear();
                foreach (PersonInfo person in people)
                    item.AddPerson(person);
            }

            if (!item.LockedFields.Contains(MetadataFields.OfficialRating))
                item.OfficialRating = item.OfficialRating ?? anidb.ContentRating ?? anilist.ContentRating ?? mal.ContentRating;

            if (!item.LockedFields.Contains(MetadataFields.Runtime))
                item.RunTimeTicks = anidb.RunTimeTicks ?? item.RunTimeTicks ?? anilist.RunTimeTicks ?? mal.RunTimeTicks;           

            if (!item.LockedFields.Contains(MetadataFields.Studios))
            {
                IEnumerable<string> studios = SelectCollection(anidb.Studios, anilist.Studios, mal.Studios, item.Studios.ToArray());
                item.Studios.Clear();
                foreach (string studio in studios)
                    item.AddStudio(studio);
            }

            item.PremiereDate = anidb.StartDate ?? anilist.StartDate ?? mal.StartDate ?? item.PremiereDate;
            item.EndDate = anidb.EndDate ?? anilist.EndDate ?? mal.EndDate ?? item.EndDate;
            item.Status = item.EndDate != null ? SeriesStatus.Ended : SeriesStatus.Continuing;
            item.AirTime = anidb.AirTime ?? anilist.AirTime ?? mal.AirTime ?? item.AirTime;
            item.AirDays = SelectCollection(anidb.AirDays, anilist.AirDays, mal.AirDays, item.AirDays).ToList();

            if (item.ProductionYear == null && item.PremiereDate != null)
            {
                item.ProductionYear = item.PremiereDate.Value.Year;
            }

            SeriesInfo mostVoted = (new[] {anidb, mal, anilist}).OrderByDescending(info => info.VoteCount ?? 0).First();
            if (item.CommunityRating == null || item.VoteCount < mostVoted.VoteCount)
            {
                item.CommunityRating = mostVoted.CommunityRating != null ? (float?)Math.Round(mostVoted.CommunityRating.Value, 1) : null;
                item.VoteCount = mostVoted.VoteCount;
            }

            if (!item.LockedFields.Contains(MetadataFields.Tags))
            {
                // only prefer our own tags if we are using enlish metadata, as our providers are only available in english

                IEnumerable<string> tags;
                if (item.GetPreferredMetadataLanguage() == "en")
                    tags = MergeCollections(mal.Tags, anilist.Tags, anidb.Tags, item.Tags.ToArray());
                else
                    tags = MergeCollections(item.Tags.ToArray(), mal.Tags, anilist.Tags, anidb.Tags);

                item.Tags.Clear();
                foreach (string tag in tags)
                    item.AddTag(tag);
            }
            
            if (!item.LockedFields.Contains(MetadataFields.Genres))
            {
                // only prefer our own genre descriptions if we are using enlish metadata, as our providers are only available in english

                IEnumerable<string> genres;
                if (item.GetPreferredMetadataLanguage() == "en")
                    genres = MergeCollections(mal.Genres, anilist.Genres, anidb.Genres, item.Genres.ToArray());
                else
                    genres = MergeCollections(item.Genres.ToArray(), mal.Genres, anilist.Genres, anidb.Genres);

                genres = GenreHelper.RemoveRedundantGenres(genres);
                genres = genres.Where(g => !"Animation".Equals(g));

                var genreList = genres as IList<string> ?? genres.ToList();

                item.Genres.Clear();
                foreach (string genre in genreList.Take(MaxGenres))
                    item.AddGenre(genre);

                item.AddGenre("Anime");

                if (!item.LockedFields.Contains(MetadataFields.Tags))
                {
                    foreach (string genre in genreList.Skip(MaxGenres))
                        item.AddTag(genre);
                }
            }
        }

        private IEnumerable<T> SelectCollection<T>(params IEnumerable<T>[] items)
        {
            return items.FirstOrDefault(l => l != null && l.Any()) ?? new List<T>();
        }

        private IEnumerable<T> MergeCollections<T>(params IEnumerable<T>[] items)
        {
            var results = new List<T>();
            foreach (var collection in items)
            {
                foreach (var item in collection)
                {
                    if (!results.Contains(item))
                        results.Add(item);
                }
            }

            return results;
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