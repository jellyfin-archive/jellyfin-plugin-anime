using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly IEnumerable<ISeriesProvider> _allProviders;
        private readonly AniDbSeriesProvider _aniDbProvider;
        private readonly AniListSeriesProvider _aniListProvider;
        private readonly ILibraryManager _library;
        private readonly ILogger _log;
        private readonly MalSeriesProvider _malProvider;
        private readonly IAniDbTitleMatcher _titleMatcher;

        public AnimeSeriesProvider(ILogManager logManager, ILibraryManager library, IServerConfigurationManager configurationManager, IApplicationPaths appPaths, IHttpClient httpClient)
            : base(logManager, configurationManager)
        {
            _log = logManager.GetLogger("AnimeSeriesProvider");
            _library = library;
            _aniDbProvider = new AniDbSeriesProvider(appPaths, httpClient);
            _malProvider = new MalSeriesProvider(new MalSeriesDownloader(appPaths, logManager.GetLogger("MalDownloader")), logManager.GetLogger("MyAnimeList"));
            _aniListProvider = new AniListSeriesProvider(new AniListSeriesDownloader(appPaths, logManager.GetLogger("AniListDownloader")), logManager.GetLogger("AniList"));
            _titleMatcher = AniDbTitleMatcher.DefaultInstance;

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
                return (MetadataProviderPriority) 6;
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

            if (!series.DontFetchMeta)
            {
                if (force || PluginConfiguration.Instance().AllowAutomaticMetadataUpdates || !item.ResolveArgs.ContainsMetaFileByName("series.xml"))
                {
                    SeriesInfo initialSeriesInfo = SeriesInfo.FromSeries(series);
                    initialSeriesInfo.ExternalProviders[ProviderNames.AniDb] = await FindAniDbId(initialSeriesInfo, GetFolderName(series), cancellationToken).ConfigureAwait(false);

                    SeriesInfo merged = await FindSeriesInfo(initialSeriesInfo, item.GetPreferredMetadataLanguage(), cancellationToken);
                    merged.Set(series);
                }
            }

            SetLastRefreshed(item, DateTime.UtcNow, providerInfo);
            return true;
        }

        public async Task<SeriesInfo> FindSeriesInfo(SeriesInfo initialSeriesInfo, string preferredMetadataLangauge, CancellationToken cancellationToken)
        {
            var providerIds = new Dictionary<string, string>(initialSeriesInfo.ExternalProviders);

            // get anidb info
            SeriesInfo anidb = await _aniDbProvider.FindSeriesInfo(providerIds, preferredMetadataLangauge, cancellationToken);
            AddProviders(providerIds, anidb.ExternalProviders);

            // get anilist info
            SeriesInfo anilist = await _aniListProvider.FindSeriesInfo(providerIds, preferredMetadataLangauge, cancellationToken);
            AddProviders(providerIds, anilist.ExternalProviders);

            // get mal info
            SeriesInfo mal = await _malProvider.FindSeriesInfo(providerIds, preferredMetadataLangauge, cancellationToken);
            AddProviders(providerIds, mal.ExternalProviders);

            SeriesInfo merged = MergeSeriesInfo(preferredMetadataLangauge, initialSeriesInfo, anidb, anilist, mal);
            return merged;
        }

        private async Task<string> FindAniDbId(SeriesInfo series, string folderName, CancellationToken cancellationToken)
        {
            string aid = series.ExternalProviders.GetOrDefault(ProviderNames.AniDb);
            if (string.IsNullOrEmpty(aid))
            {
                aid = await _titleMatcher.FindSeries(folderName, cancellationToken);

                if (string.IsNullOrEmpty(aid))
                {
                    aid = await _titleMatcher.FindSeries(series.Name, cancellationToken);
                }
                else if (AniDbTitleMatcher.GetComparableName(folderName) != AniDbTitleMatcher.GetComparableName(series.Name))
                {
                    // tvdb likely has matched a sequel to the first series, so clear some of its (invalid) data
                    series.Description = null;
                    series.Name = folderName;
                }

                _log.Debug("Identified {0} as AniDB ID {1}", series.Name, aid);
            }

            return aid;
        }

        private string GetFolderName(BaseItem series)
        {
            var directory = new DirectoryInfo(series.Path);
            return directory.Name;
        }

        private void RemoveProviderIds(BaseItem item, params string[] ids)
        {
            foreach (string id in ids)
            {
                if (!string.IsNullOrEmpty(item.GetProviderId(id)))
                    item.SetProviderId(id, null);
            }
        }

        private bool SeriesNotAnimated(Series series)
        {
            bool recognised = !string.IsNullOrEmpty(series.GetProviderId(MetadataProviders.Tvdb)) ||
                              !string.IsNullOrEmpty(series.GetProviderId(MetadataProviders.Imdb));

            bool isEnglishMetadata = string.Equals(series.GetPreferredMetadataLanguage(), "en", StringComparison.OrdinalIgnoreCase);

            return recognised && isEnglishMetadata && !series.Genres.Contains("Animation") && !series.Genres.Contains("Anime");
        }

        private bool SeriesIsIgnored(Series series)
        {
            PluginConfiguration config = PluginConfiguration.Instance();

            List<string> ignoredVirtualFolderLocations = _library.GetDefaultVirtualFolders().Where(vf => config.IgnoredVirtualFolders.Contains(vf.Name))
                                                                 .SelectMany(vf => vf.Locations)
                                                                 .ToList();

            for (Folder item = series.Parent; item != null; item = item.Parent)
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

        private SeriesInfo MergeSeriesInfo(string preferredMetadataLangauge, SeriesInfo item, SeriesInfo anidb, SeriesInfo anilist, SeriesInfo mal)
        {
            SeriesInfo mostVoted = (new[] {anidb, mal, anilist}).OrderByDescending(info => info.VoteCount ?? 0).First();

            var merged = new SeriesInfo
            {
                Name = anidb.Name ?? anilist.Name ?? mal.Name ?? item.Name,
                Description = item.Description ?? anilist.Description ?? mal.Description ?? anidb.Description,
                People = SelectCollection(anidb.People, anilist.People, mal.People, item.People).ToList(),
                ContentRating = item.ContentRating ?? anidb.ContentRating ?? anilist.ContentRating ?? mal.ContentRating,
                RunTimeTicks = anidb.RunTimeTicks ?? item.RunTimeTicks ?? anilist.RunTimeTicks ?? mal.RunTimeTicks,
                Studios = SelectCollection(anidb.Studios, anilist.Studios, mal.Studios, item.Studios).ToList(),
                StartDate = anidb.StartDate ?? anilist.StartDate ?? mal.StartDate ?? item.StartDate,
                EndDate = anidb.EndDate ?? anilist.EndDate ?? mal.EndDate ?? item.EndDate,
                AirTime = anidb.AirTime ?? anilist.AirTime ?? mal.AirTime ?? item.AirTime,
                AirDays = SelectCollection(anidb.AirDays, anilist.AirDays, mal.AirDays, item.AirDays).ToList(),
                Tags = preferredMetadataLangauge == "en" ? MergeCollections(mal.Tags, anilist.Tags, anidb.Tags, item.Tags).Distinct().ToList() : MergeCollections(item.Tags, mal.Tags, anilist.Tags, anidb.Tags).Distinct().ToList(),
                Genres = preferredMetadataLangauge == "en" ? MergeCollections(mal.Genres, anilist.Genres, anidb.Genres, item.Genres).ToList() : MergeCollections(item.Genres, mal.Genres, anilist.Genres, anidb.Genres).ToList(),
                CommunityRating = mostVoted.CommunityRating != null ? (float?) Math.Round(mostVoted.CommunityRating.Value, 1) : null,
                VoteCount = mostVoted.VoteCount
            };

            CleanupGenres(merged);
            RemoveDuplicateTags(merged);

            return merged;
        }

        private static void CleanupGenres(SeriesInfo merged)
        {
            PluginConfiguration config = PluginConfiguration.Instance();

            if (config.TidyGenreList)
            {
                merged.Genres = GenreHelper.RemoveRedundantGenres(merged.Genres)
                                           .Where(g => !"Animation".Equals(g) && !"Anime".Equals(g))
                                           .Distinct()
                                           .ToList();

                GenreHelper.TidyGenres(merged);
            }

            if (config.MaxGenres > 0)
            {
                if (config.MoveExcessGenresToTags)
                {
                    foreach (string genre in merged.Genres.Skip(config.MaxGenres - 1))
                    {
                        if (!merged.Tags.Contains(genre))
                            merged.Tags.Add(genre);
                    }
                }

                merged.Genres = merged.Genres.Take(config.MaxGenres - 1).ToList();
            }

            if (!merged.Genres.Contains("Anime"))
                merged.Genres.Add("Anime");
        }

        private static void RemoveDuplicateTags(SeriesInfo series)
        {
            for (int i = series.Tags.Count - 1; i >= 0; i--)
            {
                if (series.Genres.Contains(series.Tags[i]))
                    series.Tags.RemoveAt(i);
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
                foreach (T item in collection)
                {
                    if (!results.Contains(item))
                        results.Add(item);
                }
            }

            return results;
        }

        private void AddProviders(Dictionary<string, string> item, Dictionary<string, string> providers)
        {
            foreach (var provider in providers)
            {
                item[provider.Key] = provider.Value;
            }
        }
    }
}