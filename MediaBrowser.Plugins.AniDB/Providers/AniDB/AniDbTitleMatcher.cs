using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using MediaBrowser.Model.Logging;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
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
        
        private readonly ILogger _logger;
        private readonly IAniDbTitleDownloader _downloader;
        private readonly AsyncLock _lock;

        private Dictionary<string, string> _titles;
        
        /// <summary>
        /// Creates a new instance of the AniDbTitleMatcher class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="downloader">The AniDB title downloader.</param>
        public AniDbTitleMatcher(ILogger logger, IAniDbTitleDownloader downloader)
        {
            _logger = logger;
            _downloader = downloader;
            _lock = new AsyncLock();
        }

        public Task<string> FindSeries(string title)
        {
            return FindSeries(title, CancellationToken.None);
        }
        
        public async Task<string> FindSeries(string title, CancellationToken cancellationToken)
        {
            using (await _lock.LockAsync())
            {
                if (!IsLoaded)
                {
                    await Load(cancellationToken).ConfigureAwait(false);
                }
            }

            string aid;
            if (_titles.TryGetValue(title, out aid))
            {
                return aid;
            }
            
            title = GetComparableName(title);
            if (_titles.TryGetValue(title, out aid))
            {
                return aid;
            }

            return null;
        }

        const string Remove = "\"'!`?";
        const string Spacers = "/,.:;\\(){}[]+-_=–*";  // (there are not actually two - in the they are different char codes)

        internal static string GetComparableName(string name)
        {
            name = name.ToLower();
            name = name.Normalize(NormalizationForm.FormKD);
            var sb = new StringBuilder();
            foreach (var c in name)
            {
                if ((int)c >= 0x2B0 && (int)c <= 0x0333)
                {
                    // skip char modifier and diacritics 
                }
                else if (Remove.IndexOf(c) > -1)
                {
                    // skip chars we are removing
                }
                else if (Spacers.IndexOf(c) > -1)
                {
                    sb.Append(" ");
                }
                else if (c == '&')
                {
                    sb.Append(" and ");
                }
                else
                {
                    sb.Append(c);
                }
            }
            name = sb.ToString();
            name = name.Replace(", the", "");
            name = name.Replace("the ", " ");
            name = name.Replace(" the ", " ");

            string prevName;
            do
            {
                prevName = name;
                name = name.Replace("  ", " ");
            } while (name.Length != prevName.Length);

            return name.Trim();
        }

        public bool IsLoaded
        {
            get { return _titles != null; }
        }
        
        private async Task Load(CancellationToken cancellationToken)
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
                await _downloader.Load(cancellationToken).ConfigureAwait(false);
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

                var titlesFile = _downloader.TitlesFilePath;

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

                var comparable = (from pair in _titles
                                  let comp = GetComparableName(pair.Key)
                                  where !_titles.ContainsKey(comp)
                                  select new {Title = comp, Id = pair.Value})
                                 .ToArray();

                foreach (var pair in comparable)
                {
                    _titles[pair.Title] = pair.Id;
                }
            });
        }
    }
}