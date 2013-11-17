using System;
using System.Collections.Generic;
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
using MediaBrowser.Plugins.Anime.Configuration;

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
        private readonly SeriesIndexSearch _indexSearch;

        /// <summary>
        /// Creates a new instance of the <see cref="AniDbEpisodeProvider"/> class.
        /// </summary>
        /// <param name="configurationManager">The configuration manager.</param>
        /// <param name="httpClient">The HTTP client.</param>
        public AniDbEpisodeProvider(IServerConfigurationManager configurationManager, IHttpClient httpClient)
        {
            _configurationManager = configurationManager;
            _httpClient = httpClient;
            _indexSearch = new SeriesIndexSearch(configurationManager, httpClient);
        }

        public async Task<EpisodeInfo> FindEpisodeInfo(Episode item, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string seriesId = item.Series != null ? item.Series.GetProviderId(ProviderNames.AniDb) : null;
            if (string.IsNullOrEmpty(seriesId))
                return new EpisodeInfo();

            var seriesFolder = await FindSeriesFolder(seriesId, item.ParentIndexNumber ?? 1, cancellationToken);
            if (string.IsNullOrEmpty(seriesFolder))
                return new EpisodeInfo();

            FileInfo xml = GetEpisodeXmlFile(item, seriesFolder);
            if (!xml.Exists)
                return new EpisodeInfo();

            return ParseEpisodeXml(xml);
        }

        private async Task<string> FindSeriesFolder(string seriesId, int season, CancellationToken cancellationToken)
        {
            var seriesIndex = await _indexSearch.FindSeriesIndex(seriesId, cancellationToken);
            var seasonOffset = season - seriesIndex;
            
            if (seasonOffset != 0)
            {
                var id = await _indexSearch.FindSeriesByRelativeIndex(seriesId, seasonOffset, cancellationToken);
                return AniDbSeriesProvider.CalculateSeriesDataPath(_configurationManager.ApplicationPaths, id);
            }

            var seriesDataPath = await AniDbSeriesProvider.GetSeriesData(_configurationManager.ApplicationPaths, _httpClient, seriesId, cancellationToken);
            return Path.GetDirectoryName(seriesDataPath);
        }
        
        public bool RequiresInternet
        {
            get { return false; }
        }

        public bool NeedsRefreshBasedOnCompareDate(BaseItem item, BaseProviderInfo providerInfo)
        {
            if (!PluginConfiguration.Instance.AllowAutomaticMetadataUpdates)
            {
                return false;
            }

            var episode = (Episode) item;
            string seriesId = episode.Series != null ? episode.Series.GetProviderId(ProviderNames.AniDb) : null;

            if (!string.IsNullOrEmpty(seriesId))
            {
                string seriesDataPath = AniDbSeriesProvider.CalculateSeriesDataPath(_configurationManager.ApplicationPaths, seriesId);
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

                string title = titles.Localize(PluginConfiguration.Instance.TitlePreference, _configurationManager.Configuration.PreferredMetadataLanguage).Name;
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