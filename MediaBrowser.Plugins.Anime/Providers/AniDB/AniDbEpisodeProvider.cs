using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    /// <summary>
    /// The <see cref="AniDbEpisodeProvider"/> class provides episode metadata from AniDB.
    /// </summary>
    public class AniDbEpisodeProvider
        : IEpisodeProvider
    {
        private readonly IServerConfigurationManager _configurationManager;
        private readonly IHttpClient _httpClient;

        /// <summary>
        /// Creates a new instance of the <see cref="AniDbEpisodeProvider"/> class.
        /// </summary>
        /// <param name="configurationManager">The configuration manager.</param>
        public AniDbEpisodeProvider(IServerConfigurationManager configurationManager, IHttpClient httpClient)
        {
            _configurationManager = configurationManager;
            _httpClient = httpClient;
        }

        public async Task<EpisodeInfo> FindEpisodeInfo(Episode item, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string seriesId = item.Series != null ? item.Series.GetProviderId(ProviderNames.AniDb) : null;
            if (string.IsNullOrEmpty(seriesId))
                return new EpisodeInfo();

            FileInfo xml = GetEpisodeXmlFile(item, await FindSeriesFolder(seriesId, item.ParentIndexNumber ?? 1, cancellationToken));
            if (!xml.Exists)
                return new EpisodeInfo();

            return ParseEpisodeXml(xml);
        }

        private async Task<string> FindSeriesFolder(string seriesId, int season, CancellationToken cancellationToken)
        {
            var seriesDataPath = await AniDbSeriesProvider.GetSeriesData(_configurationManager.ApplicationPaths, _httpClient, seriesId, cancellationToken);

            if (season > 1)
            {
                return await SearchForSequel(seriesDataPath, season - 1, cancellationToken);
            }

            return Path.GetDirectoryName(seriesDataPath);
        }

        private async Task<string> SearchForSequel(string seriesDataPath, int count, CancellationToken cancellationToken)
        {
            var sequelId = FindSequel(seriesDataPath);
            if (string.IsNullOrEmpty(sequelId))
                return null;

            var sequelData = await AniDbSeriesProvider.GetSeriesData(_configurationManager.ApplicationPaths, _httpClient, sequelId, cancellationToken);
            if (ReadType(sequelData) == "TV Series")
                count--;

            if (count == 0)
                return Path.GetDirectoryName(sequelData);

            return await SearchForSequel(sequelData, count, cancellationToken);
        }

        private string ReadType(string sequelData)
        {
            var settings = new XmlReaderSettings
            {
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                ValidationType = ValidationType.None
            };

            using (var streamReader = File.Open(sequelData, FileMode.Open, FileAccess.Read))
            using (XmlReader reader = XmlReader.Create(streamReader, settings))
            {
                reader.MoveToContent();

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "type")
                    {
                        return reader.ReadElementContentAsString();
                    }
                }
            }

            return null;
        }

        private string FindSequel(string seriesPath)
        {
            var settings = new XmlReaderSettings
            {
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                ValidationType = ValidationType.None
            };

            using (var streamReader = File.Open(seriesPath, FileMode.Open, FileAccess.Read))
            using (XmlReader reader = XmlReader.Create(streamReader, settings))
            {
                reader.MoveToContent();

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "relatedanime")
                    {
                        return ReadSequelId(reader.ReadSubtree());
                    }
                }
            }

            return null;
        }

        private string ReadSequelId(XmlReader reader)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "anime")
                {
                    var id = reader.GetAttribute("id");
                    var type = reader.GetAttribute("type");

                    if (type == "Sequel")
                        return id;
                }
            }

            return null;
        }

        public bool RequiresInternet
        {
            get { return false; }
        }

        public bool NeedsRefreshBasedOnCompareDate(BaseItem item, BaseProviderInfo providerInfo)
        {
            var episode = (Episode) item;
            string seriesId = episode.Series != null ? episode.Series.GetProviderId(ProviderNames.AniDb) : null;

            if (!string.IsNullOrEmpty(seriesId))
            {
                string seriesDataPath = AniDbSeriesProvider.GetSeriesDataPath(_configurationManager.ApplicationPaths, seriesId);
                FileInfo xmlFile = GetEpisodeXmlFile(episode, seriesDataPath);

                if (xmlFile.Exists)
                {
                    return xmlFile.LastWriteTimeUtc > providerInfo.LastRefreshed;
                }
            }

            return false;
        }

        private EpisodeInfo ParseEpisodeXml(FileInfo xml)
        {
            var info = new EpisodeInfo();

            var settings = new XmlReaderSettings
            {
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                ValidationType = ValidationType.None
            };

            using (StreamReader streamReader = xml.OpenText())
            using (XmlReader reader = XmlReader.Create(streamReader, settings))
            {
                reader.MoveToContent();

                var titles = new List<Title>();

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        switch (reader.Name)
                        {
                            case "length":
                                string length = reader.ReadElementContentAsString();
                                if (!string.IsNullOrEmpty(length))
                                {
                                    long duration;
                                    if (long.TryParse(length, out duration))
                                        info.RunTimeTicks = TimeSpan.FromMinutes(duration).Ticks;
                                }

                                break;
                            case "airdate":
                                string airdate = reader.ReadElementContentAsString();
                                if (!string.IsNullOrEmpty(airdate))
                                {
                                    DateTime date;
                                    if (DateTime.TryParse(airdate, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out date))
                                        info.AirDate = date;
                                }

                                break;
                            case "rating":
                                int count;
                                float rating;
                                if (int.TryParse(reader.GetAttribute("count"), NumberStyles.Any, CultureInfo.InvariantCulture, out count) &&
                                    float.TryParse(reader.ReadElementContentAsString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out rating))
                                {
                                    info.VoteCount = count;
                                    info.CommunityRating = rating;
                                }

                                break;
                            case "title":
                                string language = reader.GetAttribute("xml:lang");
                                string name = reader.ReadElementContentAsString();

                                titles.Add(new Title
                                {
                                    Language = language,
                                    Type = "main",
                                    Name = name
                                });

                                break;
                        }
                    }
                }

                string title = titles.Localize(Configuration.Instance.TitlePreference, _configurationManager.Configuration.PreferredMetadataLanguage).Name;
                if (!string.IsNullOrEmpty(title))
                    info.Name = title;
            }

            return info;
        }

        private FileInfo GetEpisodeXmlFile(Episode episode, string seriesDataPath)
        {
            if (episode.IndexNumber == null)
            {
                return null;
            }

            string nameFormat = (episode.ParentIndexNumber == 0) ? "episode-S{0}.xml" : "episode-{0}.xml";
            string filename = Path.Combine(seriesDataPath, string.Format(nameFormat, episode.IndexNumber.Value));
            return new FileInfo(filename);
        }
    }
}