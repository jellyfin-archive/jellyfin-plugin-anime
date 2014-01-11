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
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;

namespace MediaBrowser.Plugins.Anime.Providers.MyAnimeList
{
    public interface IMalDownloader
    {
        Task<string> DownloadSeriesPage(string id);
    }

    public class MalSeriesDownloader : IMalDownloader
    {
        private const string SeriesUrl = "http://myanimelist.net/anime/{0}/";

        private readonly IApplicationPaths _appPaths;
        private readonly ILogger _logger;

        public MalSeriesDownloader(IApplicationPaths appPaths, ILogger logger)
        {
            _appPaths = appPaths;
            _logger = logger;
        }

        public async Task<string> DownloadSeriesPage(string id)
        {
            //todo MyAnimeList requires a more complete browser to load its html pages
            return null;
//            var cachedPath = Path.Combine(_appPaths.DataPath, "mal", id + ".html");
//            var cached = new FileInfo(cachedPath);
//
//            if (!cached.Exists)
//            {
//                var url = string.Format(SeriesUrl, id);
//
//                try
//                {
//                    var client = new WebClient();
//                    client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/30.0.1599.101 Safari/537.36");
//                    var data = await client.DownloadStringTaskAsync(url);
//
//                    File.WriteAllText(cachedPath, data, Encoding.UTF8);
//                    return data;
//                }
//                catch (Exception e)
//                {
//                    _logger.ErrorException("Failed to download {0}", e, url);
//                    return null;
//                }
//            }
//
//            return File.ReadAllText(cachedPath, Encoding.UTF8);
        }
    }

    public class MalSeriesProvider : ISeriesProvider
    {
        private static readonly Regex TitleRegex = new Regex(@"<h1><div style=""float: right; font-size: 13px;"">Ranked #\d+</div>(?<title>.*?)</h1>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex SummaryRegex = new Regex(@"<td valign=""top""><h2>Synopsis</h2>(?<overview>.*?)</td>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex PosterRegex = new Regex(@"<a href=""\S+""><img src=""(?<image>\S+)"" alt=""(?<title>.*?)"" align=""center""></a>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex GenreListRegex = new Regex(@"<div class=""spaceit""><span class=""dark_text"">Genres:</span>\s*(<a href=""\S+"">[\w\s]+</a>(, )?)+</div>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex GenreRegex = new Regex(@"<a href=""\S+"">(?<genre>[\w\s]+)</a>", RegexOptions.Compiled);
        //private static readonly Regex StatusRegex = new Regex(@"<div><span class=""dark_text"">Status:</span> (?<status>[\w\s]+)</div>");
        private static readonly Regex AirTimeRegex = new Regex(@"<div class=""spaceit""><span class=""dark_text"">Aired:</span> (?<from>.*?) to (?<to>.*?)</div>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex CommunityRatingRegex = new Regex(@"<h2>Statistics</h2><div><span class=""dark_text"">Score:</span> (?<score>\d+(\.\d+))(<sup><small>1</small></sup>)? <small>\(scored by (?<votes>\d+) users\)</small></div>", RegexOptions.Singleline | RegexOptions.Compiled);

        private readonly IMalDownloader _downloader;
        private readonly ILogger _logger;

        public MalSeriesProvider(IMalDownloader downloader, ILogger logger)
        {
            _downloader = downloader;
            _logger = logger;
        }

        public bool RequiresInternet { get { return true; } }

        public bool NeedsRefreshBasedOnCompareDate(BaseItem item, BaseProviderInfo providerInfo)
        {
            return false;
        }

        public async Task<SeriesInfo> FindSeriesInfo(Dictionary<string, string> providerIds , string preferredMetadataLanguage, CancellationToken cancellationToken)
        {
            var seriesId = providerIds.GetOrDefault(ProviderNames.MyAnimeList);
            if (string.IsNullOrEmpty(seriesId))
            {
                return new SeriesInfo();
            }

            try
            {
                var data = await _downloader.DownloadSeriesPage(seriesId);
                if (data == null)
                {
                    return new SeriesInfo();
                }

                var info = new SeriesInfo();
                ParseTitle(info, data);
                ParseSummary(info, data);
                ParseGenres(info, data);
                ParseAirTime(info, data);
                ParseCommunityRating(info, data);

                return info;
            }
            catch (Exception e)
            {
                _logger.ErrorException("Failed to scrape {0}", e, seriesId);
            }

            return new SeriesInfo();
        }

        private void ParseTitle(SeriesInfo info, string data)
        {
            var match = TitleRegex.Match(data);
            if (match.Success)
            {
                info.Name = match.Groups["title"].Value;
            }
        }

        private void ParseSummary(SeriesInfo info, string data)
        {
            var match = SummaryRegex.Match(data);
            if (match.Success)
            {
                var summary = match.Groups["overview"].Value;
                info.Description = StripHtml(summary);
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

        private void ParseGenres(SeriesInfo info, string data)
        {
            var genreListMatch = GenreListRegex.Match(data);
            if (genreListMatch.Success)
            {
                MatchCollection genreMatches = GenreRegex.Matches(data);
                info.Genres = (from Match m in genreMatches select m.Groups["genre"].Value).ToList();
            }
        }

        private void ParseAirTime(SeriesInfo info, string data)
        {
            var match = AirTimeRegex.Match(data);
            if (match.Success)
            {
                DateTime start;
                if (DateTime.TryParse(match.Groups["from"].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
                {
                    info.StartDate = start;
                }

                DateTime end;
                if (DateTime.TryParse(match.Groups["to"].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out end))
                {
                    info.EndDate = end;
                }
            }
        }

        private void ParseCommunityRating(SeriesInfo info, string data)
        {
            var match = CommunityRatingRegex.Match(data);
            if (match.Success)
            {
                float rating;
                int votes;

                if (float.TryParse(match.Groups["score"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out rating) &&
                    int.TryParse(match.Groups["votes"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out votes))
                {
                    info.CommunityRating = rating;
                    info.VoteCount = votes;
                }
            }
        }
    }
}
