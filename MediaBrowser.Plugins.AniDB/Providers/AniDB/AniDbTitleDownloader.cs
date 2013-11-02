using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Logging;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    /// <summary>
    /// The AniDbTitleDownloader class downloads the anime titles file from AniDB and stores it.
    /// </summary>
    public class AniDbTitleDownloader : IAniDbTitleDownloader
    {
        /// <summary>
        /// The URL for retrieving a list of all anime titles and their AniDB IDs.
        /// </summary>
        private const string TitlesUrl = "http://anidb.net/api/animetitles.xml";

        private readonly IApplicationPaths _paths;
        private readonly ILogger _logger;

        public AniDbTitleDownloader(ILogger logger, IApplicationPaths paths)
        {
            _logger = logger;
            _paths = paths;
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

        public async Task Load(CancellationToken cancellationToken)
        {
            var titlesFile = TitlesFilePath;
            var titlesFileInfo = new FileInfo(titlesFile);

            // download titles if we do not already have them, or have not updated for a week
            if (!titlesFileInfo.Exists || (DateTime.UtcNow - titlesFileInfo.LastWriteTimeUtc).TotalDays > 7)
            {
                await DownloadTitles(titlesFile, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Downloads an xml file from AniDB which contains all of the titles for every anime, and their IDs,
        /// and saves it to disk.
        /// </summary>
        /// <param name="titlesFile">The destination file name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task DownloadTitles(string titlesFile, CancellationToken cancellationToken)
        {
            _logger.Debug("Downloading new AniDB titles file.");

            var client = new WebClient();

            using (var stream = await client.OpenReadTaskAsync(TitlesUrl))
            using (var unzipped = new GZipStream(stream, CompressionMode.Decompress))
            using (var writer = File.Open(titlesFile, FileMode.Create, FileAccess.Write))
            {
                await unzipped.CopyToAsync(writer).ConfigureAwait(false);
            }
        }

        public string TitlesFilePath
        {
            get
            {
                var data = GetDataPath(_paths);
                Directory.CreateDirectory(data);

                return Path.Combine(data, "titles.xml");
            }
        }
    }
}