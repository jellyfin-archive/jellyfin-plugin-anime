using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugins.Anime.Configuration;
using MediaBrowser.Plugins.Anime.Providers.AniDB;
using MediaBrowser.Plugins.Anime.Providers.MyAnimeList;

namespace MediaBrowser.Plugins.Anime.Providers.AniList
{
    public interface IAniListDownloader
    {
        Task<FileInfo> DownloadSeriesPage(string id);
        FileInfo GetCachedSeriesPage(string id);
    }

    public class AniListSeriesDownloader : IAniListDownloader
    {
        private const string SeriesUrl = "http://anilist.co/anime/{0}/";

        private readonly IApplicationPaths _appPaths;
        private readonly ILogger _logger;

        public AniListSeriesDownloader(IApplicationPaths appPaths, ILogger logger)
        {
            _appPaths = appPaths;
            _logger = logger;
        }

        public async Task<FileInfo> DownloadSeriesPage(string id)
        {
            var cachedPath = CalculateCacheFilename(id);
            var cached = new FileInfo(cachedPath);

            if (!cached.Exists || DateTime.UtcNow - cached.LastWriteTimeUtc > TimeSpan.FromDays(7))
            {
                var url = string.Format(SeriesUrl, id);

                try
                {
                    var client = new WebClient();
                    client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/30.0.1599.101 Safari/537.36");
                    var data = await client.DownloadStringTaskAsync(url);

                    var directory = Path.GetDirectoryName(cachedPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllText(cachedPath, data, Encoding.UTF8);
                    return new FileInfo(cachedPath);
                }
                catch (Exception e)
                {
                    _logger.ErrorException("Failed to download {0}", e, url);
                }
            }

            return cached;
        }

        public FileInfo GetCachedSeriesPage(string id)
        {
            return new FileInfo(CalculateCacheFilename(id));
        }

        private string CalculateCacheFilename(string id)
        {
            return Path.Combine(_appPaths.DataPath, "anilist", id + ".html");
        }
    }

    public class AniListSeriesProvider : ISeriesProvider
    {
        private static readonly Regex RomajiTitleRegex = new Regex(@"<li><span class='type'>Romaji Title:</span><span class='value'>(?<romaji>.*?)</span></li>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex JapaneseTitleRegex = new Regex(@"li><span class='type'>Japanese:</span><span class='value'>(?<japanese>.*?)</span></li>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex EngTitleRegex = new Regex(@"<li><span class='type'>Eng Title:</span><span class='value'>(?<eng>.*?)</span></li>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex DescriptionRegex = new Regex(@"<div id=""series_des"">(?<description>.*?)</div>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex StudioRegex = new Regex(@"<li><span class='type'>Main Work:</span><span class='value'><a href='.*?'>(?<studio>.*?)</a><br></span></li>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex StartDateRegex = new Regex(@"<li><span class='type'>Start:</span><span class='value'>(?<start>.*?)</span></li>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex EndDateRegex = new Regex(@"<li><span class='type'>End:</span><span class='value'>(?<end>.*?)</span></li>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex DurationRegex = new Regex(@"<li><span class='type'>Duration:</span><span class='value'>(?<duration>\d+) mins\s*</span></li>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex GenreListRegex = new Regex(@"<li><span class='type'>Genres:</span><span class='value'>(?<genres>.*?)</span></li>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex GenreRegex = new Regex(@"(?<genre>.*?)<br>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex RatingRegex = new Regex(@"<li><span class='type'>Rating:</span><span class='value'>(?<rating>.*?)( - .*?)?</span></li>", RegexOptions.Singleline | RegexOptions.Compiled);

        private readonly IServerConfigurationManager _configurationManager;
        private readonly IAniListDownloader _downloader;
        private readonly ILogger _logger;

        public AniListSeriesProvider(IAniListDownloader downloader, ILogger logger, IServerConfigurationManager configurationManager)
        {
            _downloader = downloader;
            _logger = logger;
            _configurationManager = configurationManager;
        }

        public bool RequiresInternet { get { return true; } }

        public bool NeedsRefreshBasedOnCompareDate(BaseItem item, BaseProviderInfo providerInfo)
        {
            if (!PluginConfiguration.Instance().AllowAutomaticMetadataUpdates)
            {
                return false;
            }

            var seriesId = item.GetProviderId(ProviderNames.AniList) ?? item.GetProviderId(ProviderNames.MyAnimeList);
            if (!string.IsNullOrEmpty(seriesId))
            {
                var cached = _downloader.GetCachedSeriesPage(seriesId);
                return !cached.Exists || (DateTime.UtcNow - cached.LastWriteTimeUtc) > TimeSpan.FromDays(7);
            }

            return false;
        }

        public async Task<SeriesInfo> FindSeriesInfo(Series series, CancellationToken cancellationToken)
        {
            var seriesId = series.GetProviderId(ProviderNames.AniList) ?? series.GetProviderId(ProviderNames.MyAnimeList);
            if (string.IsNullOrEmpty(seriesId))
            {
                return new SeriesInfo();
            }

            try
            {
                var dataFile = await _downloader.DownloadSeriesPage(seriesId);
                if (!dataFile.Exists)
                {
                    return new SeriesInfo();
                }

                var data = File.ReadAllText(dataFile.FullName, Encoding.UTF8);

                var info = new SeriesInfo();
                ParseTitle(info, data);
                ParseSummary(info, data);
                ParseStudio(info, data);
                ParseGenres(info, data);
                ParseAirDates(info, data);
                ParseDuration(info, data);
                ParseRating(info, data);
                
                return info;
            }
            catch (Exception e)
            {
                _logger.ErrorException("Failed to scrape {0}", e, seriesId);
            }

            return new SeriesInfo();
        }

        private void ParseStudio(SeriesInfo info, string data)
        {
            var match = StudioRegex.Match(data);
            if (match.Success)
            {
                var studio = match.Groups["studio"].Value;
                info.Studios.Add(studio);
            }
        }

        private void ParseTitle(SeriesInfo info, string data)
        {
            var titles = new List<Title>();

            var romajiMatch = RomajiTitleRegex.Match(data);
            if (romajiMatch.Success)
            {
                var title = HttpUtility.HtmlDecode(romajiMatch.Groups["romaji"].Value);
                titles.Add(new Title
                {
                    Name = title,
                    Language = "x-jat",
                    Type = "main"
                });
            }

            var japaneseMatch = JapaneseTitleRegex.Match(data);
            if (japaneseMatch.Success)
            {
                var title = japaneseMatch.Groups["japanese"].Value;
                titles.Add(new Title
                {
                    Name = title,
                    Language = "ja",
                    Type = "main"
                });
            }

            var englishMatch = EngTitleRegex.Match(data);
            if (englishMatch.Success)
            {
                var title = englishMatch.Groups["eng"].Value;
                titles.Add(new Title
                {
                    Name = title,
                    Language = "en",
                    Type = "main"
                });
            }

            var preferredTitle = titles.Localize(PluginConfiguration.Instance().TitlePreference, _configurationManager.Configuration.PreferredMetadataLanguage);
            if (preferredTitle != null)
            {
                info.Name = preferredTitle.Name;
            }
        }

        private void ParseSummary(SeriesInfo info, string data)
        {
            var match = DescriptionRegex.Match(data);
            if (match.Success)
            {
                info.Description = MalSeriesProvider.StripHtml(HttpUtility.HtmlDecode(match.Groups["description"].Value));
            }
        }

        private void ParseGenres(SeriesInfo info, string data)
        {
            var genreListMatch = GenreListRegex.Match(data);
            if (genreListMatch.Success)
            {
                var genreMatches = GenreRegex.Matches(genreListMatch.Groups["genres"].Value);
                foreach (Match match in genreMatches)
                {
                    if (match.Success)
                    {
                        info.Genres.Add(match.Groups["genre"].Value.Trim());
                    }
                }
            }
        }

        private void ParseAirDates(SeriesInfo info, string data)
        {
            var startMatch = StartDateRegex.Match(data);
            if (startMatch.Success)
            {
                DateTime date;
                if (DateTime.TryParse(startMatch.Groups["start"].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    info.StartDate = date;
                }
            }

            var endMatch = EndDateRegex.Match(data);
            if (endMatch.Success)
            {
                DateTime date;
                if (DateTime.TryParse(endMatch.Groups["end"].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    info.EndDate = date;
                }
            }
        }

        private void ParseDuration(SeriesInfo info, string data)
        {
            var match = DurationRegex.Match(data);
            if (match.Success)
            {
                int duration;
                if (int.TryParse(match.Groups["duration"].Value.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out duration))
                {
                    info.RunTimeTicks = TimeSpan.FromMinutes(duration).Ticks;
                }
            }
        }

        private void ParseRating(SeriesInfo info, string data)
        {
            var match = RatingRegex.Match(data);
            if (match.Success)
            {
                info.ContentRating = match.Groups["rating"].Value;
            }
        }
    }
}
