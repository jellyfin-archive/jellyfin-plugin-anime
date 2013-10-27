using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Logging;

namespace MediaBrowser.Plugins.AniDB
{
    /// <summary>
    /// The <see cref="IAniDbTitleMatcher"/> interface defines a type which can match series titles to AniDB IDs.
    /// </summary>
    public interface IAniDbTitleMatcher
    {
        /// <summary>
        /// Finds the AniDB for the series with the given title.
        /// </summary>
        /// <param name="title">The title of the series to search for.</param>
        /// <returns>The AniDB ID of the series is found; else <c>null</c>.</returns>
        Task<string> FindSeries(string title);

        /// <summary>
        /// Loads series titles from the series.xml file into memory.
        /// </summary>
        Task Load();
    }

    /// <summary>
    /// The <see cref="AniDbTitleMatcher"/> class loads series titles from the series.xml file in the application data anidb folder,
    /// and provides the means to search for a the AniDB of a series by series title.
    /// </summary>
    public class AniDbTitleMatcher : IAniDbTitleMatcher
    {
        //todo replace the singleton IAniDbTitleMatcher with an injected dependency if/when MediaBrowser allows plugins to register their own components
        /// <summary>
        /// Gets or sets the global <see cref="IAniDbTitleMatcher"/> instance.
        /// </summary>
        public static IAniDbTitleMatcher DefaultInstance { get; set; }

        private readonly IApplicationPaths _paths;
        private readonly ILogger _logger;

        private Dictionary<string, string> _titles;

        /// <summary>
        /// Creates a new instance of the AniDbTitleMatcher class.
        /// </summary>
        /// <param name="paths">The application paths.</param>
        /// <param name="logger">The logger.</param>
        public AniDbTitleMatcher(IApplicationPaths paths, ILogger logger)
        {
            _paths = paths;
            _logger = logger;
        }

        /// <summary>
        /// Gets the path to the anidb data folder.
        /// </summary>
        /// <param name="applicationPaths">The application paths.</param>
        /// <returns>The path to the anidb data folder.</returns>
        public static string GetDataPath(IApplicationPaths applicationPaths)
        {
            return Path.Combine(applicationPaths.DataPath, "anidb");
        }

        /// <summary>
        /// Gets the path to the titles.xml file.
        /// </summary>
        /// <param name="applicationPaths">The application paths.</param>
        /// <returns>The path to the titles.xml file.</returns>
        public static string GetTitlesFile(IApplicationPaths applicationPaths)
        {
            var data = GetDataPath(applicationPaths);
            Directory.CreateDirectory(data);

            return Path.Combine(data, "titles.xml");
        }

        public async Task<string> FindSeries(string title)
        {
            if (!IsLoaded)
            {
                await Load().ConfigureAwait(false);
            }

            string aid;
            if (_titles.TryGetValue(title, out aid))
            {
                return aid;
            }

            return null;
        }

        public bool IsLoaded
        {
            get { return _titles != null; }
        }
        
        public async Task Load()
        {
            if (_titles == null)
            {
                _titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _titles.Clear();
            }

            try
            {
                await ReadTitlesFile().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.ErrorException("Failed to load AniDB titles", e);
            }
        }

        private Task ReadTitlesFile()
        {
            return Task.Run(() =>
            {
                _logger.Debug("Loading AniDB titles");

                var titlesFile = GetTitlesFile(_paths);

                var settings = new XmlReaderSettings
                {
                    CheckCharacters = false,
                    IgnoreProcessingInstructions = true,
                    IgnoreComments = true,
                    ValidationType = ValidationType.None
                };

                using (var stream = new StreamReader(titlesFile, Encoding.UTF8))
                using (var reader = XmlReader.Create(stream, settings))
                {
                    string aid = null;

                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            switch (reader.Name)
                            {
                                case "anime":
                                    reader.MoveToAttribute("aid");
                                    aid = reader.Value;
                                    break;
                                case "title":
                                    var title = reader.ReadElementContentAsString();
                                    if (!string.IsNullOrEmpty(aid) && !string.IsNullOrEmpty(title))
                                    {
                                        _titles[title] = aid;
                                    }
                                    break;
                            }
                        }
                    }
                }
            });
        }
    }
}