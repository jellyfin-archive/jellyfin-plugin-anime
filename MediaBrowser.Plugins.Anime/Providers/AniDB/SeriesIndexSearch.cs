using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using System.Linq;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    public class SeriesIndexSearch
    {
        private readonly IServerConfigurationManager _configurationManager;
        private readonly IHttpClient _httpClient;
        private readonly Dictionary<string, string[]> _cache;
        private readonly AsyncLock _lock;

        public SeriesIndexSearch(IServerConfigurationManager configurationManager, IHttpClient httpClient)
        {
            _configurationManager = configurationManager;
            _httpClient = httpClient;
            _cache = new Dictionary<string, string[]>();
            _lock = new AsyncLock();
        }

        private class SeriesDateId
        {
            public string SeriesId { get; set; }
            public DateTime? Date { get; set; }
        }

        /// <summary>
        /// Gets the index of a series among a sequence of prequels/sequels, by air date order.
        /// The first series is index 1.
        /// </summary>
        /// <param name="anidbId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<int> FindSeriesIndex(string anidbId, CancellationToken cancellationToken)
        {
            var sequence = await GetSeriesSequence(anidbId, cancellationToken);
            return Array.IndexOf(sequence, anidbId) + 1;
        }

        /// <summary>
        /// Gets the AniDB ID of a series prequel or sequel relative to the given series.
        /// </summary>
        /// <param name="anidbId">The series to start searching from.</param>
        /// <param name="offset">The series number offset to look for.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<string> FindSeriesByRelativeIndex(string anidbId, int offset, CancellationToken cancellationToken)
        {
            var sequence = await GetSeriesSequence(anidbId, cancellationToken);
            var index = Array.IndexOf(sequence, anidbId);

            return sequence[Math.Max(0, Math.Min(index + offset, sequence.Length - 1))];
        }

        private async Task<string[]> GetSeriesSequence(string anidbId, CancellationToken cancellationToken)
        {
            string[] sequence;

            using (await _lock.LockAsync())
            {
                if (!_cache.TryGetValue(anidbId, out sequence))
                {
                    sequence = await FindSeriesSequence(anidbId, cancellationToken);
                    _cache.Add(anidbId, sequence);
//                    foreach (var series in sequence)
//                    {
//                        _cache.Add(series, sequence);
//                    }
                }
            }

            return sequence;
        }

        private async Task<string[]> FindSeriesSequence(string anidbId, CancellationToken cancellationToken)
        {
            var items = new List<SeriesDateId>();

            // find prequels
            var id = anidbId;
            while (id != null)
            {
                id = await FindSeriesByLogicalRelativeIndex(id, -1, cancellationToken);
                if (id != null)
                {
                    var data = await AniDbSeriesProvider.GetSeriesData(_configurationManager.ApplicationPaths, _httpClient, id, cancellationToken);
                    var date = ReadDate(data);

                    items.Add(new SeriesDateId
                    {
                        SeriesId = id,
                        Date = date
                    });
                }
            }

            items.Reverse();

            // read current series
            var seriesData = await AniDbSeriesProvider.GetSeriesData(_configurationManager.ApplicationPaths, _httpClient, anidbId, cancellationToken);
            items.Add(new SeriesDateId
            {
                SeriesId = anidbId,
                Date = ReadDate(seriesData)
            });
            
            // find sequels
            id = anidbId;
            while (id != null)
            {
                id = await FindSeriesByLogicalRelativeIndex(id, 1, cancellationToken);
                if (id != null)
                {
                    var data = await AniDbSeriesProvider.GetSeriesData(_configurationManager.ApplicationPaths, _httpClient, id, cancellationToken);
                    var date = ReadDate(data);

                    items.Add(new SeriesDateId
                    {
                        SeriesId = id,
                        Date = date
                    });
                }
            }

            // fill in any missing dates, preserving the logical ordering
            var previousDate = new DateTime(0);
            foreach (var series in items)
            {
                if (series.Date == null)
                    series.Date = previousDate + TimeSpan.FromSeconds(1);

                previousDate = series.Date.Value;
            }

            // sort by air date
            items.Sort((a, b) => a.Date.Value.CompareTo(b.Date.Value));
            return items.Select(s => s.SeriesId).ToArray();
        }

        /// <summary>
        /// Finds the path to the data folder of the series at a logical offset from that provided, in terms of
        /// prequel/sequel series.
        /// </summary>
        /// <param name="anidbSeriesId"></param>
        /// <param name="indexOffset"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private Task<string> FindSeriesByLogicalRelativeIndex(string anidbSeriesId, int indexOffset, CancellationToken cancellationToken)
        {
            string dataPath = AniDbSeriesProvider.CalculateSeriesDataPath(_configurationManager.ApplicationPaths, anidbSeriesId);
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

        private DateTime? ReadDate(string seriesData)
        {
            var settings = new XmlReaderSettings
            {
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                ValidationType = ValidationType.None
            };

            using (FileStream streamReader = File.Open(seriesData, FileMode.Open, FileAccess.Read))
            using (XmlReader reader = XmlReader.Create(streamReader, settings))
            {
                reader.MoveToContent();

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "startdate")
                    {
                        var val = reader.ReadElementContentAsString();

                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            DateTime date;
                            if (DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out date))
                            {
                                return date.ToUniversalTime();
                            }
                        }
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