using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    public class SeriesIndexSearch
    {
        private readonly IServerConfigurationManager _configurationManager;
        private readonly IHttpClient _httpClient;

        public SeriesIndexSearch(IServerConfigurationManager configurationManager, IHttpClient httpClient)
        {
            _configurationManager = configurationManager;
            _httpClient = httpClient;
        }

        public async Task<int> FindSeriesIndex(string anidbId, CancellationToken cancellationToken)
        {
            int index = 1;
            while (true)
            {
                anidbId = await FindSeriesByRelativeIndex(anidbId, -1, cancellationToken);
                if (anidbId != null)
                {
                    index++;
                }
                else
                {
                    break;
                }
            }

            return index;
        }

        public Task<string> FindSeriesByRelativeIndex(string anidbSeriesId, int indexOffset, CancellationToken cancellationToken)
        {
            string dataPath = AniDbSeriesProvider.GetSeriesDataPath(_configurationManager.ApplicationPaths, anidbSeriesId);
            if (indexOffset == 0)
                return Task.FromResult(dataPath);

            return FindRelated(dataPath, indexOffset, cancellationToken);
        }

        private async Task<string> FindRelated(string seriesDataPath, int indexOffset, CancellationToken cancellationToken)
        {
            string relatedId = FindRelated(seriesDataPath, indexOffset > 0 ? "Sequel" : "Prequel");
            if (string.IsNullOrEmpty(relatedId))
                return null;

            string relatedData = await AniDbSeriesProvider.GetSeriesData(_configurationManager.ApplicationPaths, _httpClient, relatedId, cancellationToken);
            if (ReadType(relatedData) == "TV Series")
                indexOffset = (indexOffset > 0) ? indexOffset - 1 : indexOffset + 1;

            if (indexOffset == 0)
                return Path.GetDirectoryName(relatedData);

            return await FindRelated(Path.GetDirectoryName(relatedData), indexOffset, cancellationToken);
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

            using (FileStream streamReader = File.Open(sequelData, FileMode.Open, FileAccess.Read))
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

        private string FindRelated(string seriesPath, string type)
        {
            var settings = new XmlReaderSettings
            {
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                ValidationType = ValidationType.None
            };

            using (FileStream streamReader = File.Open(Path.Combine(seriesPath, "series.xml"), FileMode.Open, FileAccess.Read))
            using (XmlReader reader = XmlReader.Create(streamReader, settings))
            {
                reader.MoveToContent();

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "relatedanime")
                    {
                        return ReadId(reader.ReadSubtree(), type);
                    }
                }
            }

            return null;
        }

        private string ReadId(XmlReader reader, string desiredType)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "anime")
                {
                    string id = reader.GetAttribute("id");
                    string type = reader.GetAttribute("type");

                    if (type == desiredType)
                        return id;
                }
            }

            return null;
        }
    }
}