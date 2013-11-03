using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    public class AniDbSeriesProvider : ISeriesProvider
    {
        private const string SeriesDataFile = "series.xml";
        private const string SeriesQueryUrl = "http://api.anidb.net:9001/httpapi?request=anime&client={0}&clientver=1&protover=1&aid={1}";
        private const string ClientName = "xbmcscrap"; // pretend to be the xbmc scraper until we can register our own application

        private readonly ILogger _log;
        private readonly IServerConfigurationManager _configurationManager;
        private readonly IApplicationPaths _appPaths;
        private readonly IHttpClient _httpClient;

        public static readonly SemaphoreSlim ResourcePool = new SemaphoreSlim(2, 2);

        private readonly Dictionary<string, string> _typeMappings = new Dictionary<string, string>
        {
            { "Direction", PersonType.Director },
            { "Music", PersonType.Composer },
            { "Chief Animation Direction", "Chief Animation Director" }
        };

        public AniDbSeriesProvider(ILogger log, IServerConfigurationManager configurationManager, IApplicationPaths appPaths, IHttpClient httpClient)
        {
            _log = log;
            _configurationManager = configurationManager;
            _appPaths = appPaths;
            _httpClient = httpClient;

            TitleMatcher = AniDbTitleMatcher.DefaultInstance;
        }

        public IAniDbTitleMatcher TitleMatcher
        {
            get;
            set;
        }

        public async Task<SeriesInfo> FindSeriesInfo(Series item, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // find aid
            var aid = item.GetProviderId(ProviderNames.AniDb);
            if (string.IsNullOrEmpty(aid))
            {
                aid = await TitleMatcher.FindSeries(item.Name, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var series = new SeriesInfo();
            series.ExternalProviders.Add(ProviderNames.AniDb, aid);

            if (!string.IsNullOrEmpty(aid))
            {
                _log.Debug("Identified {0} as AniDB ID {1}", item.Name, aid);
                var seriesDataPath = await GetSeriesData(_appPaths, _httpClient, aid, cancellationToken);

                // load series data and apply to item
                FetchSeriesInfo(series, seriesDataPath, cancellationToken);
            }

            return series;
        }

        public static async Task<string> GetSeriesData(IApplicationPaths appPaths, IHttpClient httpClient, string seriesId, CancellationToken cancellationToken)
        {
            var dataPath = GetSeriesDataPath(appPaths, seriesId);
            var seriesDataPath = Path.Combine(dataPath, SeriesDataFile);
            var fileInfo = new FileInfo(seriesDataPath);
            
            // download series data if not present, or out of date
            if (!fileInfo.Exists || DateTime.UtcNow - fileInfo.LastWriteTimeUtc > TimeSpan.FromDays(7))
            {
                await DownloadSeriesData(seriesId, seriesDataPath, httpClient, cancellationToken).ConfigureAwait(false);
            }

            return seriesDataPath;
        }

        public static string GetSeriesDataPath(IApplicationPaths paths, string seriesId)
        {
            return Path.Combine(paths.DataPath, "anidb", seriesId);
        }

        private void FetchSeriesInfo(SeriesInfo series, string seriesDataPath, CancellationToken cancellationToken)
        {
            var settings = new XmlReaderSettings
            {
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                ValidationType = ValidationType.None
            };
            
            using (var streamReader = File.Open(seriesDataPath, FileMode.Open, FileAccess.Read))
            using (var reader = XmlReader.Create(streamReader, settings))
            {
                reader.MoveToContent();

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        switch (reader.Name)
                        {
                            case "startdate":
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    DateTime date;
                                    if (DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out date))
                                    {
                                        date = date.ToUniversalTime();
                                        series.StartDate = date;
                                    }
                                }

                                break;
                            case "enddate":
                                var endDate = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(endDate))
                                {
                                    DateTime date;
                                    if (DateTime.TryParse(endDate, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out date))
                                    {
                                        date = date.ToUniversalTime();
                                        series.EndDate = date;
                                    }
                                }

                                break;
                            case "titles":
                                using (var subtree = reader.ReadSubtree())
                                {
                                    var title = ParseTitle(subtree);
                                    if (!string.IsNullOrEmpty(title))
                                    {
                                        series.Name = title;
                                    }
                                }

                                break;
                            case "creators":
                                using (var subtree = reader.ReadSubtree())
                                {
                                    ParseCreators(series, subtree);
                                }

                                break;
                            case "description":
                                if (string.IsNullOrEmpty(series.Description))
                                {
                                    series.Description = StripAniDbLinks(reader.ReadElementContentAsString());
                                }

                                break;
                            case "ratings":
                                using (var subtree = reader.ReadSubtree())
                                {
                                    ParseRatings(series, subtree);
                                }

                                break;
                            case "resources":
                                using (var subtree = reader.ReadSubtree())
                                {
                                    ParseResources(series, subtree);
                                }

                                break;
                            case "characters":
                                using (var subtree = reader.ReadSubtree())
                                {
                                    ParseActors(series, subtree);
                                }

                                break;
                            case "tags":
                                using (var subtree = reader.ReadSubtree())
                                {
                                }

                                break;
                            case "categories":
                                using (var subtree = reader.ReadSubtree())
                                {
                                }

                                break;
                        }
                    }
                }
            }
        }

        private void ParseResources(SeriesInfo series, XmlReader reader)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "resource")
                {
                    var type = reader.GetAttribute("type");

                    switch (type)
                    {
                        case "2":
                            while (reader.Read())
                            {
                                if (reader.NodeType == XmlNodeType.Element && reader.Name == "identifier")
                                {
                                    series.ExternalProviders.Add(ProviderNames.MyAnimeList, reader.ReadElementContentAsString());
                                    break;
                                }
                            }
                            
                            break;
                    }
                }
            }
        }

        private static readonly Regex AniDbUrlRegex = new Regex(@"http://anidb.net/\w+ \[(?<name>[^\]]*)\]");

        private string StripAniDbLinks(string text)
        {
            return AniDbUrlRegex.Replace(text, "${name}");
        }

        private void ParseActors(SeriesInfo series, XmlReader reader)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.Name == "character")
                    {
                        using (var subtree = reader.ReadSubtree())
                        {
                            ParseActor(series, subtree);
                        }
                    }
                }
            }
        }

        private void ParseActor(SeriesInfo series, XmlReader reader)
        {
            string name = null;
            string role = null;

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "name":
                            role = reader.ReadElementContentAsString();
                            break;
                        case "seiyuu":
                            name = reader.ReadElementContentAsString();
                            break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(role))
            {
                series.People.Add(CreatePerson(name, PersonType.Actor, role));
            }
        }

        private void ParseRatings(SeriesInfo series, XmlReader reader)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.Name == "permanent")
                    {
                        int count;
                        if (int.TryParse(reader.GetAttribute("count"), NumberStyles.Any, CultureInfo.InvariantCulture, out count))
                            series.VoteCount = count;
                        
                        float rating;
                        if (float.TryParse(
                            reader.ReadElementContentAsString(),
                            NumberStyles.AllowDecimalPoint,
                            CultureInfo.InvariantCulture,
                            out rating))
                        {
                            series.CommunityRating = rating;
                        }
                    }
                }
            }
        }

        private string ParseTitle(XmlReader reader)
        {
            var titles = new List<Title>();

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "title")
                {
                    var language = reader.GetAttribute("xml:lang");
                    var type = reader.GetAttribute("type");
                    var name = reader.ReadElementContentAsString();
                    
                    titles.Add(new Title
                    {
                        Language = language,
                        Type = type,
                        Name = name
                    });
                }
            }

            return titles.Localize(Configuration.Instance.TitlePreference, _configurationManager.Configuration.PreferredMetadataLanguage).Name;
        }

        private void ParseCreators(SeriesInfo series, XmlReader reader)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "name")
                {
                    var type = reader.GetAttribute("type");
                    var name = reader.ReadElementContentAsString();

                    if (type == "Animation Work")
                    {
                        series.Studios.Add(name);
                    }
                    else
                    {
                        series.People.Add(CreatePerson(name, type));
                    }
                }
            }
        }

        private PersonInfo CreatePerson(string name, string type, string role = null)
        {
            // todo find nationality of person and conditionally reverse name order

            string mappedType;
            if (!_typeMappings.TryGetValue(type, out mappedType))
            {
                mappedType = type;
            }

            return new PersonInfo
            {
                Name = ReverseNameOrder(name),
                Type = mappedType,
                Role = role
            };
        }

        public static string ReverseNameOrder(string name)
        {
            return name.Split(' ').Reverse().Aggregate(string.Empty, (n, part) => n + " " + part).Trim();
        }

        private static async Task DownloadSeriesData(string aid, string seriesDataPath, IHttpClient httpClient, CancellationToken cancellationToken)
        {
            var directory = Path.GetDirectoryName(seriesDataPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            DeleteXmlFiles(directory);
            
            var requestOptions = new HttpRequestOptions
            {
                Url = string.Format(SeriesQueryUrl, ClientName, aid),
                CancellationToken = cancellationToken,
                EnableHttpCompression = false
            };

            using (var stream = await httpClient.Get(requestOptions).ConfigureAwait(false))
            using (var unzipped = new GZipStream(stream, CompressionMode.Decompress))
            using (var file = File.Open(seriesDataPath, FileMode.Create, FileAccess.Write))
            {
                await unzipped.CopyToAsync(file).ConfigureAwait(false);
            }

            await ExtractEpisodes(directory, seriesDataPath);
            await ExtractCast(directory, seriesDataPath);
        }

        private static void DeleteXmlFiles(string path)
        {
            try
            {
                foreach (var file in new DirectoryInfo(path)
                    .EnumerateFiles("*.xml", SearchOption.AllDirectories)
                    .ToList())
                {
                    file.Delete();
                }
            }
            catch (DirectoryNotFoundException)
            {
                // No biggie
            }
        }

        private static async Task ExtractEpisodes(string seriesDataDirectory, string seriesDataPath)
        {
            var settings = new XmlReaderSettings
            {
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                ValidationType = ValidationType.None
            };

            using (var streamReader = new StreamReader(seriesDataPath, Encoding.UTF8))
            {
                // Use XmlReader for best performance
                using (var reader = XmlReader.Create(streamReader, settings))
                {
                    reader.MoveToContent();

                    // Loop through each element
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            if (reader.Name == "episode")
                            {
                                var outerXml = reader.ReadOuterXml();
                                await SaveEpsiodeXml(seriesDataDirectory, outerXml).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
        }

        private static async Task ExtractCast(string seriesDataDirectory, string seriesDataPath)
        {
            var settings = new XmlReaderSettings
            {
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                ValidationType = ValidationType.None
            };

            using (var streamReader = new StreamReader(seriesDataPath, Encoding.UTF8))
            {
                // Use XmlReader for best performance
                using (var reader = XmlReader.Create(streamReader, settings))
                {
                    reader.MoveToContent();

                    // Loop through each element
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "characters")
                        {
                            var outerXml = reader.ReadOuterXml();
                            await SaveXml(outerXml, Path.Combine(seriesDataDirectory, "cast.xml")).ConfigureAwait(false);
                            break;
                        }
                    }
                }
            }
        }

        private static async Task SaveXml(string xml, string filename)
        {
            var writerSettings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Async = true
            };

            using (var writer = XmlWriter.Create(filename, writerSettings))
            {
                await writer.WriteRawAsync(xml).ConfigureAwait(false);
            }
        }

        private static async Task SaveEpsiodeXml(string seriesDataDirectory, string xml)
        {
            var episodeNumber = ParseEpisodeNumber(xml);

            if (episodeNumber != null)
            {
                var file = Path.Combine(seriesDataDirectory, string.Format("episode-{0}.xml", episodeNumber));
                await SaveXml(xml, file);
            }
        }

        private static string ParseEpisodeNumber(string xml)
        {
            var settings = new XmlReaderSettings
            {
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                ValidationType = ValidationType.None
            };
            
            using (var streamReader = new StringReader(xml))
            {
                // Use XmlReader for best performance
                using (var reader = XmlReader.Create(streamReader, settings))
                {
                    reader.MoveToContent();

                    // Loop through each element
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            if (reader.Name == "epno")
                            {
                                var val = reader.ReadElementContentAsString();
                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    return val;
                                }
                            }
                            else
                            {
                                reader.Skip();
                            }
                        }
                    }
                }
            }

            return null;
        }

        public bool RequiresInternet
        {
            get { return true; }
        }

        public bool NeedsRefreshBasedOnCompareDate(BaseItem item, BaseProviderInfo providerInfo)
        {
            var seriesId = item.GetProviderId(MetadataProviders.Tvdb);

            if (!string.IsNullOrEmpty(seriesId))
            {
                var path = Path.Combine(_appPaths.DataPath, "anidb", seriesId);

                try
                {
                    var files = new DirectoryInfo(path)
                        .EnumerateFiles("*.xml", SearchOption.TopDirectoryOnly)
                        .Select(i => i.LastWriteTimeUtc)
                        .ToList();

                    if (files.Count > 0)
                    {
                        return files.Max() > providerInfo.LastRefreshed;
                    }
                }
                catch (DirectoryNotFoundException)
                {
                    // Don't blow up
                    return true;
                }
            }

            return false;
        }
    }

    public enum TitlePreferenceType
    {
        Localized,
        Japanese,
        JapaneseRomaji,
    }

    public class Title
    {
        public string Language { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
    }

    public static class TitleExtensions
    {
        public static Title Localize(this IEnumerable<Title> titles, TitlePreferenceType preference, string metadataLanguage)
        {
            var titlesList = titles as IList<Title> ?? titles.ToList();

            if (preference == TitlePreferenceType.Localized)
            {
                // prefer an official title, else look for a synonym
                var localized = titlesList.FirstOrDefault(t => t.Language == metadataLanguage && t.Type == "main") ??
                                titlesList.FirstOrDefault(t => t.Language == metadataLanguage && t.Type == "official") ??
                                titlesList.FirstOrDefault(t => t.Language == metadataLanguage && t.Type == "synonym");

                if (localized != null)
                {
                    return localized;
                }
            }

            if (preference == TitlePreferenceType.Japanese)
            {
                // prefer an official title, else look for a synonym
                var japanese = titlesList.FirstOrDefault(t => t.Language == "ja" && t.Type == "main") ??
                               titlesList.FirstOrDefault(t => t.Language == "ja" && t.Type == "official") ??
                               titlesList.FirstOrDefault(t => t.Language == "ja" && t.Type == "synonym");

                if (japanese != null)
                {
                    return japanese;
                }
            }

            // return the main title (romaji)
            return titlesList.FirstOrDefault(t => t.Language == "x-jat" && t.Type == "main") ??
                   titlesList.FirstOrDefault(t => t.Type == "main") ??
                   titlesList.FirstOrDefault();
        }
    }
}
