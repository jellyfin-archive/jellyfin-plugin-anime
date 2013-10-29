using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Implementations.HttpClientManager;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugins.AniDB.Providers;
using Moq;
using NUnit.Framework;

namespace MediaBrowser.Plugins.AniDB.Tests
{
    [TestFixture]
    public class AniDbSeriesProviderTests
    {
        [Test]
        public async Task MatchSeries()
        {
            var paths = new Mock<IServerApplicationPaths>();
            paths.Setup(p => p.DataPath).Returns("TestData");

            var logger = new Mock<ILogger>();
            var logManager = new Mock<ILogManager>();

            var configurationManager = new Mock<IServerConfigurationManager>();
            configurationManager.Setup(c => c.ApplicationPaths).Returns(paths.Object);
            configurationManager.Setup(c => c.Configuration).Returns(new Model.Configuration.ServerConfiguration());

            var downloader = new Mock<IAniDbTitleDownloader>();
            downloader.Setup(d => d.Load(It.IsAny<CancellationToken>())).Returns(Task.FromResult(0));
            downloader.Setup(d => d.TitlesFilePath).Returns("TestData/anidb/titles.xml");

            var httpClient = new HttpClientManager(paths.Object, logger.Object, CreateHttpClient);
            
            AniDbTitleMatcher.DefaultInstance = new AniDbTitleMatcher(logger.Object, downloader.Object);

            var provider = new AniDbSeriesProvider(logManager.Object, configurationManager.Object, paths.Object, httpClient);

            var series = new Series
            {
                Name = "Mahou Shoujo Madoka Magica"
            };

            var cancellation = new CancellationTokenSource();

            await provider.FetchAsync(series, false, cancellation.Token);

            Assert.That(series.Name, Is.EqualTo("Puella Magi Madoka Magica"));
        }

        HttpClient CreateHttpClient(bool enableHttpCompression)
        {
            return new HttpClient(new WebRequestHandler
            {
                CachePolicy = new RequestCachePolicy(RequestCacheLevel.Revalidate),
                AutomaticDecompression = enableHttpCompression ? DecompressionMethods.Deflate | DecompressionMethods.GZip : DecompressionMethods.None
            })
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
        }
    }
}
