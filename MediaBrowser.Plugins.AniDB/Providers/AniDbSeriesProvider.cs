using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
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
    public class AniDbSeriesProvider : BaseMetadataProvider
    {
        public const string AniDbProviderId = "AniDB";
        
        private const string SeriesDataFile = "series.xml";
        private const string SeriesQueryUrl = "http://api.anidb.net:9001/httpapi?request=anime&client={0}&clientver=1&protover=1&aid={1}";
        private const string ClientName = "xbmcscrap"; // pretend to be the xbmc scraper until we can register our own application

        private readonly IApplicationPaths _appPaths;
        private readonly IHttpClient _httpClient;

        private readonly Dictionary<string, string> _typeMappings = new Dictionary<string, string>
        {
            { "Direction", PersonType.Director },
            { "Music", PersonType.Composer },
            { "Chief Animation Direction", "Chief Animation Director" }
        };

        public AniDbSeriesProvider(ILogManager logManager, IServerConfigurationManager configurationManager, IApplicationPaths appPaths, IHttpClient httpClient) 
            : base(logManager, configurationManager)
        {
            _appPaths = appPaths;
            _httpClient = httpClient;
            TitleMatcher = AniDbTitleMatcher.DefaultInstance;
        }

        public IAniDbTitleMatcher TitleMatcher
        {
            get;
            set;
        }

        public TitlePreferenceType TitlePreference
        {
            get;
            set;
        }

        public override bool Supports(BaseItem item)
        {
            return item is Series;
        }

        public override async Task<bool> FetchAsync(BaseItem item, bool force, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var series = (Series)item;
            
            // find aid
            var aid = series.GetProviderId(AniDbProviderId);
            if (string.IsNullOrEmpty(aid))
            {
                aid = await TitleMatcher.FindSeries(series.Name, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrEmpty(aid))
            {
                // download series data if not present
                var dataPath = Path.Combine(_appPaths.DataPath, "anidb", aid);
                var seriesDataPath = Path.Combine(dataPath, SeriesDataFile);

                if (!File.Exists(seriesDataPath))
                {
                    await DownloadSeriesData(aid, seriesDataPath, cancellationToken).ConfigureAwait(false);
                }

                // load series data and apply to item
                if (!series.DontFetchMeta)
                {
                    FetchSeriesInfo(series, seriesDataPath, cancellationToken);
                }
            }

            SetLastRefreshed(item, DateTime.UtcNow);
            return true;
        }

        private void FetchSeriesInfo(Series series, string seriesDataPath, CancellationToken cancellationToken)
        {
            // name, description, air day, rating (and count), consumer rating, status, start date, run time, studio, generes

            var settings = new XmlReaderSettings
            {
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                ValidationType = ValidationType.None
            };
            
            using (var streamReader = new StreamReader(seriesDataPath, Encoding.UTF8))
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

                                        series.PremiereDate = date;
                                        series.ProductionYear = date.Year;
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
                                if (!series.LockedFields.Contains(MetadataFields.Name))
                                {
                                    using (var subtree = reader.ReadSubtree())
                                    {
                                        var title = ParseTitle(subtree);
                                        if (!string.IsNullOrEmpty(title))
                                        {
                                            series.Name = title;
                                        }
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
                                // prefer tbdb overview
                                if (string.IsNullOrEmpty(series.Overview) && !series.LockedFields.Contains(MetadataFields.Overview))
                                {
                                    series.Overview = StripAniDbLinks(reader.ReadElementContentAsString());
                                }

                                break;
                            case "ratings":
                                using (var subtree = reader.ReadSubtree())
                                {
                                    ParseRatings(series, subtree);
                                }

                                break;
                            case "resources":
                            case "characters":
                                using (var subtree = reader.ReadSubtree())
                                {
                                    ParseActors(series, subtree);
                                }

                                break;
                        }
                    }
                }
            }

            series.Status = series.EndDate != null ? SeriesStatus.Ended : SeriesStatus.Continuing;
        }

        private static readonly Regex AniDbUrlRegex = new Regex(@"http://anidb.net/\w+ \[(?<name>[^\]]*)\]");

        private string StripAniDbLinks(string text)
        {
            return AniDbUrlRegex.Replace(text, "${name}");
        }

        private void ParseActors(Series series, XmlReader reader)
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

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(role))
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

            if (TitlePreference == TitlePreferenceType.Localized)
            {
                var targetLanguage = ConfigurationManager.Configuration.PreferredMetadataLanguage.ToLower();

                // prefer an official title, else look for a synonym
                var localized =
                    titles.FirstOrDefault(t => t.Language == targetLanguage && t.Type == "official") ??
                    titles.FirstOrDefault(t => t.Language == targetLanguage && t.Type == "synonym");

                if (localized != null)
                {
                    return localized.Name;
                }
            }

            if (TitlePreference == TitlePreferenceType.Japanese)
            {
                // prefer an official title, else look for a synonym
                var japanese =
                    titles.FirstOrDefault(t => t.Language == "ja" && t.Type == "official") ??
                    titles.FirstOrDefault(t => t.Language == "ja" && t.Type == "synonym");

                if (japanese != null)
                {
                    return japanese.Name;
                }
            }

            // return the main title (romaji)
            return titles.First(t => t.Type == "main").Name;
        }

        public enum TitlePreferenceType
        {
            Localized,
            Japanese,
            JapaneseRomaji,
        }

        private class Title
        {
            public string Language { get; set; }
            public string Type { get; set; }
            public string Name { get; set; }
        }

        private void ParseCreators(Series series, XmlReader reader)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "name")
                {
                    var type = reader.GetAttribute("type");
                    var name = reader.ReadElementContentAsString();

                    if (type == "Animation Work")
                    {
                        if (!series.LockedFields.Contains(MetadataFields.Studios))
                            series.AddStudio(name);
                    }
                    else
                    {
                        if (!series.LockedFields.Contains(MetadataFields.Cast))
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

        private string ReverseNameOrder(string name)
        {
            return name.Split(' ').Reverse().Aggregate(string.Empty, (n, part) => n + " " + part).Trim();
        }

        private async Task DownloadSeriesData(string aid, string seriesDataPath, CancellationToken cancellationToken)
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

            using (var stream = await _httpClient.Get(requestOptions).ConfigureAwait(false))
            using (var unzipped = new GZipStream(stream, CompressionMode.Decompress))
            using (var file = File.Open(seriesDataPath, FileMode.Create, FileAccess.Write))
            {
                await unzipped.CopyToAsync(file).ConfigureAwait(false);
            }

            await ExtractEpisodes(directory, seriesDataPath);
        }

        private void DeleteXmlFiles(string path)
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

        private async Task ExtractEpisodes(string seriesDataDirectory, string seriesDataPath)
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

        private async Task SaveEpsiodeXml(string seriesDataDirectory, string xml)
        {
            var episodeNumber = ParseEpisodeNumber(xml);

            if (episodeNumber != null)
            {
                var file = Path.Combine(seriesDataDirectory, string.Format("episode-{0}.xml", episodeNumber));

                var writerSettings = new XmlWriterSettings
                {
                    Encoding = Encoding.UTF8,
                    Async = true
                };

                using (var writer = XmlWriter.Create(file, writerSettings))
                {
                    await writer.WriteRawAsync(xml).ConfigureAwait(false);
                }
            }
        }

        private static int? ParseEpisodeNumber(string xml)
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
                                    int num;
                                    if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture,
                                        out num))
                                    {
                                        return num;
                                    }
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

        protected override bool NeedsRefreshBasedOnCompareDate(BaseItem item, BaseProviderInfo providerInfo)
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

            return base.NeedsRefreshBasedOnCompareDate(item, providerInfo);
        }

        public override bool EnforceDontFetchMetadata
        {
            get
            {
                return false;
            }
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
    }
}
