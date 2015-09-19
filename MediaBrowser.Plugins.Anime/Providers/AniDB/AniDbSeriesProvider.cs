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
using System.Xml.Serialization;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Plugins.Anime.Configuration;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    public class AniDbSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        private const string SeriesDataFile = "series.xml";
        private const string SeriesQueryUrl = "http://api.anidb.net:9001/httpapi?request=anime&client={0}&clientver=1&protover=1&aid={1}";
        private const string ClientName = "mediabrowser";

        private const string TvdbSeriesOffset = "TvdbSeriesOffset";
        private const string TvdbSeriesOffsetFormat = "{0}-{1}";
        internal static AniDbSeriesProvider Current { get; private set; }

        // AniDB has very low request rate limits, a minimum of 2 seconds between requests, and an average of 4 seconds between requests
        public static readonly SemaphoreSlim ResourcePool = new SemaphoreSlim(1, 1);
        public static readonly RateLimiter RequestLimiter = new RateLimiter(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5));

        private static readonly int[] IgnoredCategoryIds = {6, 22, 23, 60, 128, 129, 185, 216, 242, 255, 268, 269, 289};
        private static readonly Regex AniDbUrlRegex = new Regex(@"http://anidb.net/\w+ \[(?<name>[^\]]*)\]");
        private readonly IApplicationPaths _appPaths;
        private readonly IHttpClient _httpClient;
        private readonly ILibraryManager _libraryManager;
        private readonly SeriesIndexSearch _indexSearcher;

        private readonly Dictionary<string, string> _typeMappings = new Dictionary<string, string>
        {
            {"Direction", PersonType.Director},
            {"Music", PersonType.Composer},
            {"Chief Animation Direction", "Chief Animation Director"}
        };

        public AniDbSeriesProvider(IApplicationPaths appPaths, IHttpClient httpClient, IServerConfigurationManager configurationManager, ILibraryManager libraryManager)
        {
            _appPaths = appPaths;
            _httpClient = httpClient;
            _indexSearcher = new SeriesIndexSearch(configurationManager, httpClient);
            _libraryManager =  libraryManager;

            TitleMatcher = AniDbTitleMatcher.DefaultInstance;

            Current = this;
        }

        public IAniDbTitleMatcher TitleMatcher { get; set; }
        
        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>();

            string aid = info.ProviderIds.GetOrDefault(ProviderNames.AniDb);
            if (string.IsNullOrEmpty(aid))
                aid = await TitleMatcher.FindSeries(info.Name, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(aid))
            {
                result.Item = new Series();
                result.HasMetadata = true;

                result.Item.ProviderIds.Add(ProviderNames.AniDb, aid);
                result.Item.ProviderIds.Add(ProviderNames.AniDbOriginalSeries, await _indexSearcher.FindOriginalSeriesId(aid, cancellationToken));

                string seriesDataPath = await GetSeriesData(_appPaths, _httpClient, aid, cancellationToken);
                FetchSeriesInfo(result.Item, seriesDataPath, info.MetadataLanguage ?? "en");
            }

            return result;
        }

        public string Name
        {
            get { return "AniDB"; }
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            var seriesId = searchInfo.GetProviderId(ProviderNames.AniDb);

            var metadata = await GetMetadata(searchInfo, cancellationToken).ConfigureAwait(false);

            var list = new List<RemoteSearchResult>();

            if (metadata.HasMetadata)
            {
                var res = new RemoteSearchResult
                {
                    Name = metadata.Item.Name,
                    PremiereDate = metadata.Item.PremiereDate,
                    ProductionYear = metadata.Item.ProductionYear,
                    ProviderIds = metadata.Item.ProviderIds,
                    SearchProviderName = Name
                };

                list.Add(res);
            }

            return list;
            //return Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public static async Task<string> GetSeriesData(IApplicationPaths appPaths, IHttpClient httpClient, string seriesId, CancellationToken cancellationToken)
        {
            string dataPath = CalculateSeriesDataPath(appPaths, seriesId);
            string seriesDataPath = Path.Combine(dataPath, SeriesDataFile);
            var fileInfo = new FileInfo(seriesDataPath);

            // download series data if not present, or out of date
            if (!fileInfo.Exists || DateTime.UtcNow - fileInfo.LastWriteTimeUtc > TimeSpan.FromDays(7))
            {
                await DownloadSeriesData(seriesId, seriesDataPath, httpClient, cancellationToken).ConfigureAwait(false);
            }

            return seriesDataPath;
        }

        public static string CalculateSeriesDataPath(IApplicationPaths paths, string seriesId)
        {
            return Path.Combine(paths.CachePath, "anidb", "series", seriesId);
        }

        private void FetchSeriesInfo(Series series, string seriesDataPath, string preferredMetadataLangauge)
        {
            var settings = new XmlReaderSettings
            {
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                ValidationType = ValidationType.None
            };

            using (FileStream streamReader = File.Open(seriesDataPath, FileMode.Open, FileAccess.Read))
            using (XmlReader reader = XmlReader.Create(streamReader, settings))
            {
                reader.MoveToContent();

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        switch (reader.Name)
                        {
                            case "startdate":
                                string val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    DateTime date;
                                    if (DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out date))
                                    {
                                        date = date.ToUniversalTime();
                                        series.PremiereDate = date;
                                    }
                                }

                                break;
                            case "enddate":
                                string endDate = reader.ReadElementContentAsString();

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
                                using (XmlReader subtree = reader.ReadSubtree())
                                {
                                    string title = ParseTitle(subtree, preferredMetadataLangauge);
                                    if (!string.IsNullOrEmpty(title))
                                    {
                                        series.Name = title;
                                    }
                                }

                                break;
                            case "creators":
                                using (XmlReader subtree = reader.ReadSubtree())
                                {
                                    ParseCreators(series, subtree);
                                }

                                break;
                            case "description":
                                series.Overview = ReplaceLineFeedWithNewLine(StripAniDbLinks(reader.ReadElementContentAsString()));

                                break;
                            case "ratings":
                                using (XmlReader subtree = reader.ReadSubtree())
                                {
                                    ParseRatings(series, subtree);
                                }

                                break;
                            case "resources":
                                using (XmlReader subtree = reader.ReadSubtree())
                                {
                                    ParseResources(series, subtree);
                                }

                                break;
                            case "characters":
                                using (XmlReader subtree = reader.ReadSubtree())
                                {
                                    ParseActors(series, subtree);
                                }

                                break;
                            case "tags":
                                using (XmlReader subtree = reader.ReadSubtree())
                                {
                                }

                                break;
                            case "categories":
                                using (XmlReader subtree = reader.ReadSubtree())
                                {
                                    ParseCategories(series, subtree);
                                }

                                break;
                            case "episodes":
                                using (XmlReader subtree = reader.ReadSubtree())
                                {
                                    ParseEpisodes(series, subtree);
                                }

                                break;
                        }
                    }
                }
            }

            GenreHelper.TidyGenres(series);
            GenreHelper.RemoveDuplicateTags(series);
        }

        private void ParseEpisodes(Series series, XmlReader reader)
        {
            var episodes = new List<EpisodeInfo>();

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "episode")
                {
                    int id;
                    if (int.TryParse(reader.GetAttribute("id"), out id) && IgnoredCategoryIds.Contains(id))
                        continue;

                    using (XmlReader episodeSubtree = reader.ReadSubtree())
                    {
                        while (episodeSubtree.Read())
                        {
                            if (episodeSubtree.NodeType == XmlNodeType.Element)
                            {
                                switch (episodeSubtree.Name)
                                {
                                    case "epno":
                                        string epno = episodeSubtree.ReadElementContentAsString();
                                        //EpisodeInfo info = new EpisodeInfo();
                                        //info.AnimeSeriesIndex = series.AnimeSeriesIndex;
                                        //info.IndexNumberEnd = string(epno);
                                        //info.SeriesProviderIds.GetOrDefault(ProviderNames.AniDb);
                                        //episodes.Add(info);
                                        break;
                                }
                            }
                        }
                    }
                }
            }

            //series.Genres = genres.OrderBy(g => g.Weight).Select(g => g.Name).ToList();
        }

        private void ParseCategories(Series series, XmlReader reader)
        {
            var genres = new List<GenreInfo>();

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "category")
                {
                    int weight;
                    if (!int.TryParse(reader.GetAttribute("weight"), out weight) || weight < 400)
                        continue;

                    int id;
                    if (int.TryParse(reader.GetAttribute("id"), out id) && IgnoredCategoryIds.Contains(id))
                        continue;

                    int parentId;
                    if (int.TryParse(reader.GetAttribute("parentid"), out parentId) && IgnoredCategoryIds.Contains(parentId))
                        continue;

                    using (XmlReader categorySubtree = reader.ReadSubtree())
                    {
                        while (categorySubtree.Read())
                        {
                            if (categorySubtree.NodeType == XmlNodeType.Element && categorySubtree.Name == "name")
                            {
                                string name = categorySubtree.ReadElementContentAsString();
                                genres.Add(new GenreInfo {Name = name, Weight = weight});
                            }
                        }
                    }
                }
            }

            series.Genres = genres.OrderBy(g => g.Weight).Select(g => g.Name).ToList();
        }

        private void ParseResources(Series series, XmlReader reader)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "resource")
                {
                    string type = reader.GetAttribute("type");

                    switch (type)
                    {
                        case "2":
                            var ids = new List<int>();

                            using (var idSubtree = reader.ReadSubtree())
                            {
                                while (idSubtree.Read())
                                {
                                    if (idSubtree.NodeType == XmlNodeType.Element && idSubtree.Name == "identifier")
                                    {
                                        int id;
                                        if (int.TryParse(idSubtree.ReadElementContentAsString(), out id))
                                            ids.Add(id);
                                    }
                                }
                            }

                            if (ids.Count > 0)
                            {
                                var firstId = ids.OrderBy(i => i).First().ToString(CultureInfo.InvariantCulture);
                                series.ProviderIds.Add(ProviderNames.MyAnimeList, firstId);
                                series.ProviderIds.Add(ProviderNames.AniList, firstId);
                            }

                            break;
                        case "4":
                            while (reader.Read())
                            {
                                if (reader.NodeType == XmlNodeType.Element && reader.Name == "url")
                                {
                                    series.HomePageUrl = reader.ReadElementContentAsString();
                                    break;
                                }
                            }

                            break;
                    }
                }
            }
        }

        private string StripAniDbLinks(string text)
        {
            return AniDbUrlRegex.Replace(text, "${name}");
        }

        public static string ReplaceLineFeedWithNewLine(string text)
        {
            return text.Replace("\n", Environment.NewLine);
        }

        private void ParseActors(Series series, XmlReader reader)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.Name == "character")
                    {
                        using (XmlReader subtree = reader.ReadSubtree())
                        {
                            ParseActor(series, subtree);
                        }
                    }
                }
            }
        }

        private void ParseActor(Series series, XmlReader reader)
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

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(role)) // && series.People.All(p => p.Name != name))
            {
                series.AddPerson(CreatePerson(name, PersonType.Actor, role));
            }
        }

        private void ParseRatings(Series series, XmlReader reader)
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
                            series.CommunityRating = (float)Math.Round(rating, 1);
                        }
                    }
                }
            }
        }

        private string ParseTitle(XmlReader reader, string preferredMetadataLangauge)
        {
            var titles = new List<Title>();

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "title")
                {
                    string language = reader.GetAttribute("xml:lang");
                    string type = reader.GetAttribute("type");
                    string name = reader.ReadElementContentAsString();

                    titles.Add(new Title
                    {
                        Language = language,
                        Type = type,
                        Name = name
                    });
                }
            }

            return titles.Localize(PluginConfiguration.Instance().TitlePreference, preferredMetadataLangauge).Name;
        }

        private void ParseCreators(Series series, XmlReader reader)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "name")
                {
                    string type = reader.GetAttribute("type");
                    string name = reader.ReadElementContentAsString();

                    if (type == "Animation Work")
                    {
                        series.Studios.Add(name);
                    }
                    else
                    {
                        series.AddPerson(CreatePerson(name, type));
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
            string directory = Path.GetDirectoryName(seriesDataPath);
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

            await RequestLimiter.Tick();

            using (Stream stream = await httpClient.Get(requestOptions).ConfigureAwait(false))
            using (var unzipped = new GZipStream(stream, CompressionMode.Decompress))
            using (var reader = new StreamReader(unzipped, Encoding.UTF8, true))
            using (FileStream file = File.Open(seriesDataPath, FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(file))
            {
                string text = await reader.ReadToEndAsync().ConfigureAwait(false);
                text = text.Replace("&#x0;", "");

                await writer.WriteAsync(text).ConfigureAwait(false);
            }

            await ExtractEpisodes(directory, seriesDataPath);
            ExtractCast(directory, seriesDataPath);
        }

        private static void DeleteXmlFiles(string path)
        {
            try
            {
                foreach (FileInfo file in new DirectoryInfo(path)
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
                using (XmlReader reader = XmlReader.Create(streamReader, settings))
                {
                    reader.MoveToContent();

                    // Loop through each element
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            if (reader.Name == "episode")
                            {
                                string outerXml = reader.ReadOuterXml();
                                await SaveEpsiodeXml(seriesDataDirectory, outerXml).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
        }

        private static void ExtractCast(string seriesDataDirectory, string seriesDataPath)
        {
            var settings = new XmlReaderSettings
            {
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                ValidationType = ValidationType.None
            };

            var list = new CastList();

            using (var streamReader = new StreamReader(seriesDataPath, Encoding.UTF8))
            {
                // Use XmlReader for best performance
                using (XmlReader reader = XmlReader.Create(streamReader, settings))
                {
                    reader.MoveToContent();

                    // Loop through each element
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "characters")
                        {
                            string outerXml = reader.ReadOuterXml();
                            list.Cast.AddRange(ParseCharacterList(outerXml));
                        }

                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "creators")
                        {
                            string outerXml = reader.ReadOuterXml();
                            list.Cast.AddRange(ParseCreatorsList(outerXml));
                        }
                    }
                }
            }

            var serializer = new XmlSerializer(typeof (CastList));
            using (FileStream stream = File.Open(Path.Combine(seriesDataDirectory, "cast.xml"), FileMode.Create, FileAccess.Write))
            {
                serializer.Serialize(stream, list);
            }
        }

        private static IEnumerable<AniDbPersonInfo> ParseCharacterList(string xml)
        {
            XDocument doc = XDocument.Parse(xml);
            var people = new List<AniDbPersonInfo>();

            XElement characters = doc.Element("characters");
            if (characters != null)
            {
                foreach (XElement character in characters.Descendants("character"))
                {
                    XElement seiyuu = character.Element("seiyuu");
                    if (seiyuu != null)
                    {
                        var person = new AniDbPersonInfo
                        {
                            Name = seiyuu.Value
                        };

                        XAttribute picture = seiyuu.Attribute("picture");
                        if (picture != null && !string.IsNullOrEmpty(picture.Value))
                        {
                            person.Image = "http://img7.anidb.net/pics/anime/" + picture.Value;
                        }

                        XAttribute id = seiyuu.Attribute("id");
                        if (id != null && !string.IsNullOrEmpty(id.Value))
                        {
                            person.Id = id.Value;
                        }

                        people.Add(person);
                    }
                }
            }

            return people;
        }

        private static IEnumerable<AniDbPersonInfo> ParseCreatorsList(string xml)
        {
            XDocument doc = XDocument.Parse(xml);
            var people = new List<AniDbPersonInfo>();

            XElement creators = doc.Element("creators");
            if (creators != null)
            {
                foreach (XElement creator in creators.Descendants("name"))
                {
                    XAttribute type = creator.Attribute("type");
                    if (type != null && type.Value == "Animation Work")
                    {
                        continue;
                    }

                    var person = new AniDbPersonInfo
                    {
                        Name = creator.Value
                    };

                    XAttribute id = creator.Attribute("id");
                    if (id != null && !string.IsNullOrEmpty(id.Value))
                    {
                        person.Id = id.Value;
                    }

                    people.Add(person);
                }
            }

            return people;
        }

        private static async Task SaveXml(string xml, string filename)
        {
            var writerSettings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Async = true
            };

            using (XmlWriter writer = XmlWriter.Create(filename, writerSettings))
            {
                await writer.WriteRawAsync(xml).ConfigureAwait(false);
            }
        }

        private static async Task SaveEpsiodeXml(string seriesDataDirectory, string xml)
        {
            string episodeNumber = ParseEpisodeNumber(xml);

            if (episodeNumber != null)
            {
                string file = Path.Combine(seriesDataDirectory, string.Format("episode-{0}.xml", episodeNumber));
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
                using (XmlReader reader = XmlReader.Create(streamReader, settings))
                {
                    reader.MoveToContent();

                    // Loop through each element
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            if (reader.Name == "epno")
                            {
                                string val = reader.ReadElementContentAsString();
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

        private struct GenreInfo
        {
            public string Name;
            public int Weight;
        }

        public int Order { get { return -1; } }

        /// <summary>
        /// Gets the series data path.
        /// </summary>
        /// <param name="appPaths">The app paths.</param>
        /// <param name="seriesId">The series id.</param>
        /// <returns>System.String.</returns>
        internal static string GetSeriesDataPath(IApplicationPaths appPaths, string seriesId)
        {
            var seriesDataPath = Path.Combine(GetSeriesDataPath(appPaths), seriesId);

            return seriesDataPath;
        }

        /// <summary>
        /// Gets the series data path.
        /// </summary>
        /// <param name="appPaths">The app paths.</param>
        /// <returns>System.String.</returns>
        internal static string GetSeriesDataPath(IApplicationPaths appPaths)
        {
            var dataPath = Path.Combine(appPaths.CachePath, "anidb\\series");

            return dataPath;
        }

        internal static int? GetSeriesOffset(Dictionary<string, string> seriesProviderIds)
        {
            string idString;
            if (!seriesProviderIds.TryGetValue(TvdbSeriesOffset, out idString))
                return null;

            var parts = idString.Split('-');
            if (parts.Length < 2)
                return null;

            int offset;
            if (int.TryParse(parts[1], out offset))
                return offset;

            return null;
        }
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
            IList<Title> titlesList = titles as IList<Title> ?? titles.ToList();

            if (preference == TitlePreferenceType.Localized)
            {
                // prefer an official title, else look for a synonym
                Title localized = titlesList.FirstOrDefault(t => t.Language == metadataLanguage && t.Type == "main") ??
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
                Title japanese = titlesList.FirstOrDefault(t => t.Language == "ja" && t.Type == "main") ??
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
         /// <summary>
        /// Gets the series data path.
        /// </summary>
        /// <param name="appPaths">The app paths.</param>
        /// <param name="seriesId">The series id.</param>
        /// <returns>System.String.</returns>
        internal static string GetSeriesDataPath(IApplicationPaths appPaths, string seriesId)
        {
            var seriesDataPath = Path.Combine(GetSeriesDataPath(appPaths), seriesId);

            return seriesDataPath;
        }

        /// <summary>
        /// Gets the series data path.
        /// </summary>
        /// <param name="appPaths">The app paths.</param>
        /// <returns>System.String.</returns>
        internal static string GetSeriesDataPath(IApplicationPaths appPaths)
        {
            var dataPath = Path.Combine(appPaths.CachePath, "tvdb");

            return dataPath;
        }

   }
}