using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugins.Anime.Providers.AniDB.Metadata;
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB.Identity
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
        public static string s_paths;

        public AniDbTitleDownloader(ILogger logger, IApplicationPaths paths)
        {
            _logger = logger;
            _paths = paths;
            s_paths = GetDataPath(paths);
        }

        /// <summary>
        /// Gets the path to the anidb data folder.
        /// </summary>
        /// <param name="applicationPaths">The application paths.</param>
        /// <returns>The path to the anidb data folder.</returns>
        public static string GetDataPath(IApplicationPaths applicationPaths)
        {
            return Path.Combine(applicationPaths.CachePath, "anidb");
        }

        /// <summary>
        /// Load XML static| Too prevent EXCEPTIONS
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task Load_static(CancellationToken cancellationToken)
        {
            var titlesFile = TitlesFilePath_;
            var titlesFileInfo = new FileInfo(titlesFile);

            // download titles if we do not already have them, or have not updated for a week
            if (!titlesFileInfo.Exists || (DateTime.UtcNow - titlesFileInfo.LastWriteTimeUtc).TotalDays > 7)
            {
                await DownloadTitles_static(titlesFile).ConfigureAwait(false);
            }
        }

        public async Task Load(CancellationToken cancellationToken)
        {
            var titlesFile = TitlesFilePath;
            var titlesFileInfo = new FileInfo(titlesFile);

            // download titles if we do not already have them, or have not updated for a week
            if (!titlesFileInfo.Exists || (DateTime.UtcNow - titlesFileInfo.LastWriteTimeUtc).TotalDays > 7)
            {
                await DownloadTitles(titlesFile).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Downloads an xml file from AniDB which contains all of the titles for every anime, and their IDs,
        /// and saves it to disk.
        /// </summary>
        /// <param name="titlesFile">The destination file name.</param>
        private async Task DownloadTitles(string titlesFile)
        {
            _logger.Debug("Downloading new AniDB titles file.");

            var client = new WebClient();

            await AniDbSeriesProvider.RequestLimiter.Tick();
            await Task.Run(() => Thread.Sleep(Plugin.Instance.Configuration.AniDB_wait_time));
            using (var stream = await client.OpenReadTaskAsync(TitlesUrl))
            using (var unzipped = new GZipStream(stream, CompressionMode.Decompress))
            using (var writer = File.Open(titlesFile, FileMode.Create, FileAccess.Write))
            {
                await unzipped.CopyToAsync(writer).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// static|Downloads an xml file from AniDB which contains all of the titles for every anime, and their IDs,
        /// and saves it to disk.
        /// </summary>
        /// <param name="titlesFile"></param>
        /// <returns></returns>
        private static async Task DownloadTitles_static(string titlesFile)
        {
            var client = new WebClient();

            await AniDbSeriesProvider.RequestLimiter.Tick();
            await Task.Run(() => Thread.Sleep(Plugin.Instance.Configuration.AniDB_wait_time));
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
                Directory.CreateDirectory(s_paths);

                return Path.Combine(s_paths, "titles.xml");
            }
        }

        /// <summary>
        /// Get the FilePath
        /// </summary>
        public static string TitlesFilePath_
        {
            get
            {
                Directory.CreateDirectory(s_paths);

                return Path.Combine(s_paths, "titles.xml");
            }
        }
    }
}