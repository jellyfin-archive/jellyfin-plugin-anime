using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Plugins.Anime.Configuration;
using MediaBrowser.Plugins.Anime.Providers.AniDB;

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
            string cachedPath = CalculateCacheFilename(id);
            var cached = new FileInfo(cachedPath);

            if (!cached.Exists || DateTime.UtcNow - cached.LastWriteTimeUtc > TimeSpan.FromDays(7))
            {
                string url = string.Format(SeriesUrl, id);

                try
                {
                    var client = new WebClient();
                    client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/30.0.1599.101 Safari/537.36");
                    string data = await client.DownloadStringTaskAsync(url);

                    string directory = Path.GetDirectoryName(cachedPath);
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

    public class AniListSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>
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

        private readonly IApplicationPaths _appPaths;
        private readonly IAniListDownloader _downloader;
        private readonly ILogManager _logManager;
        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;

        public AniListSeriesProvider(IAniListDownloader downloader, ILogManager logManager, IApplicationPaths appPaths, IHttpClient httpClient)
        {
            _downloader = downloader;
            _logManager = logManager;
            _logger = logManager.GetLogger("AniList");
            _appPaths = appPaths;
            _httpClient = httpClient;
        }

        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>();

            string seriesId = info.ProviderIds.GetOrDefault(ProviderNames.AniList) ?? info.ProviderIds.GetOrDefault(ProviderNames.MyAnimeList);
            if (string.IsNullOrEmpty(seriesId))
            {
                // ask the AniDB provider to see if it can find the IDs
                var aniDbProvider = new AniDbSeriesProvider(_appPaths, _httpClient, _logManager);
                MetadataResult<Series> aniDbResult = await aniDbProvider.GetMetadata(info, cancellationToken).ConfigureAwait(false);

                if (!aniDbResult.HasMetadata || aniDbResult.Item == null)
                    return result;

                seriesId = aniDbResult.Item.ProviderIds.GetOrDefault(ProviderNames.AniList) ?? aniDbResult.Item.ProviderIds.GetOrDefault(ProviderNames.MyAnimeList);
            }

            if (string.IsNullOrEmpty(seriesId))
                return result;

            try
            {
                FileInfo dataFile = await _downloader.DownloadSeriesPage(seriesId);
                if (!dataFile.Exists)
                    return result;

                string data = File.ReadAllText(dataFile.FullName, Encoding.UTF8);

                result.Item = new Series();
                result.HasMetadata = true;

                ParseTitle(result.Item, data, info.MetadataLanguage);
                ParseSummary(result.Item, data);
                ParseStudio(result.Item, data);
                ParseGenres(result.Item, data);
                ParseAirDates(result.Item, data);
                ParseDuration(result.Item, data);
                ParseRating(result.Item, data);
            }
            catch (Exception e)
            {
                _logger.ErrorException("Failed to scrape {0}", e, seriesId);
            }

            return result;
        }

        public string Name
        {
            get { return "AniList"; }
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private void ParseStudio(Series info, string data)
        {
            Match match = StudioRegex.Match(data);
            if (match.Success)
            {
                string studio = match.Groups["studio"].Value;
                info.Studios.Add(studio);
            }
        }

        private void ParseTitle(Series info, string data, string preferredMetadataLanguage)
        {
            var titles = new List<Title>();

            Match romajiMatch = RomajiTitleRegex.Match(data);
            if (romajiMatch.Success)
            {
                string title = HttpUtility.HtmlDecode(romajiMatch.Groups["romaji"].Value);
                titles.Add(new Title
                {
                    Name = title,
                    Language = "x-jat",
                    Type = "main"
                });
            }

            Match japaneseMatch = JapaneseTitleRegex.Match(data);
            if (japaneseMatch.Success)
            {
                string title = japaneseMatch.Groups["japanese"].Value;
                titles.Add(new Title
                {
                    Name = title,
                    Language = "ja",
                    Type = "main"
                });
            }

            Match englishMatch = EngTitleRegex.Match(data);
            if (englishMatch.Success)
            {
                string title = englishMatch.Groups["eng"].Value;
                titles.Add(new Title
                {
                    Name = title,
                    Language = "en",
                    Type = "main"
                });
            }

            Title preferredTitle = titles.Localize(PluginConfiguration.Instance().TitlePreference, preferredMetadataLanguage);
            if (preferredTitle != null)
            {
                info.Name = preferredTitle.Name;
            }
        }

        private void ParseSummary(Series info, string data)
        {
            Match match = DescriptionRegex.Match(data);
            if (match.Success)
            {
                info.Overview = StripHtml(HttpUtility.HtmlDecode(match.Groups["description"].Value));
            }
        }

        public static string StripHtml(string source)
        {
            var array = new char[source.Length];
            int arrayIndex = 0;
            bool inside = false;

            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '<')
                {
                    inside = true;
                    continue;
                }

                if (c == '>')
                {
                    inside = false;
                    continue;
                }

                if (!inside)
                {
                    array[arrayIndex] = c;
                    arrayIndex++;
                }
            }

            return new string(array, 0, arrayIndex);
        }

        private void ParseGenres(Series info, string data)
        {
            Match genreListMatch = GenreListRegex.Match(data);
            if (genreListMatch.Success)
            {
                MatchCollection genreMatches = GenreRegex.Matches(genreListMatch.Groups["genres"].Value);
                foreach (Match match in genreMatches)
                {
                    if (match.Success)
                    {
                        info.Genres.Add(match.Groups["genre"].Value.Trim());
                    }
                }
            }
        }

        private void ParseAirDates(Series info, string data)
        {
            Match startMatch = StartDateRegex.Match(data);
            if (startMatch.Success)
            {
                DateTime date;
                if (DateTime.TryParse(startMatch.Groups["start"].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    info.PremiereDate = date;
                }
            }

            Match endMatch = EndDateRegex.Match(data);
            if (endMatch.Success)
            {
                DateTime date;
                if (DateTime.TryParse(endMatch.Groups["end"].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    info.EndDate = date;
                }
            }
        }

        private void ParseDuration(Series info, string data)
        {
            Match match = DurationRegex.Match(data);
            if (match.Success)
            {
                int duration;
                if (int.TryParse(match.Groups["duration"].Value.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out duration))
                {
                    info.RunTimeTicks = TimeSpan.FromMinutes(duration).Ticks;
                }
            }
        }

        private void ParseRating(Series info, string data)
        {
            Match match = RatingRegex.Match(data);
            if (match.Success)
            {
                info.OfficialRating = match.Groups["rating"].Value;
            }
        }
    }
}