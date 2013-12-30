using System;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Implementations.HttpClientManager;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugins.Anime.Configuration;
using MediaBrowser.Plugins.Anime.Providers.AniDB;
using Moq;
using NUnit.Framework;

namespace MediaBrowser.Plugins.Anime.Tests
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

            var downloader = new Mock<IAniDbTitleDownloader>();
            downloader.Setup(d => d.Load(It.IsAny<CancellationToken>())).Returns(Task.FromResult(0));
            downloader.Setup(d => d.TitlesFilePath).Returns("TestData/anidb/titles.xml");

            var httpClient = new HttpClientManager(paths.Object, logger.Object, CreateHttpClient);
            
            AniDbTitleMatcher.DefaultInstance = new AniDbTitleMatcher(logger.Object, downloader.Object);

            var config = new PluginConfiguration
            {
                TitlePreference = TitlePreferenceType.Localized
            };

            PluginConfiguration.Instance = () => config;

            var provider = new AniDbSeriesProvider(logger.Object, paths.Object, httpClient);

            var series = new Series
            {
                Name = "Mahou Shoujo Madoka Magica",
                Path = "TV/Mahou Shoujo Madoka Magica"
            };

            var cancellation = new CancellationTokenSource();

            var info = await provider.FindSeriesInfo(series, "en", cancellation.Token);

            Assert.That(info.Name, Is.EqualTo("Puella Magi Madoka Magica"));
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
