using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;

namespace MediaBrowser.Plugins.AniDB
{
    /// <summary>
    /// The AniDbPrescanTask class is a library prescan task which loads series titles from AniDB.
    /// </summary>
    public class AniDbPrescanTask : ILibraryPrescanTask
    {
        /// <summary>
        /// The URL for retrieving a list of all anime titles and their AniDB IDs.
        /// </summary>
        private const string TitlesUrl = "http://anidb.net/api/animetitles.xml";

        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly IServerConfigurationManager _config;

        /// <summary>
        /// Creates a new instance of the <see cref="AniDbPrescanTask"/> class.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="httpClient"></param>
        /// <param name="logger"></param>
        public AniDbPrescanTask(IServerConfigurationManager config, IHttpClient httpClient, ILogger logger)
        {
            _config = config;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task Run(IProgress<double> progress, CancellationToken cancellationToken)
        {
            if (!_config.Configuration.EnableInternetProviders)
            {
                progress.Report(100);
                return;
            }

            var titlesFile = AniDbTitleMatcher.GetTitlesFile(_config.CommonApplicationPaths);
            var titlesFileInfo = new FileInfo(titlesFile);

            // download titles if we do not already have them, or have not updated for a week
            if (!titlesFileInfo.Exists || (DateTime.UtcNow - titlesFileInfo.LastWriteTimeUtc).TotalDays > 7)
            {
                await DownloadTitles(titlesFile, cancellationToken).ConfigureAwait(false);
            }

            await AniDbTitleMatcher.DefaultInstance.Load().ConfigureAwait(false);
            progress.Report(100);
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

            var request = new HttpRequestOptions
            {
                Url = TitlesUrl,
                CancellationToken = cancellationToken,
                EnableHttpCompression = true
            };

            using (var stream = await _httpClient.Get(request).ConfigureAwait(false))
            using (var writer = File.Open(titlesFile, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(writer).ConfigureAwait(false);
            }
        }
    }
}
