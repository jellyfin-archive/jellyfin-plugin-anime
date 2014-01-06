using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugins.Anime.Configuration;
using MediaBrowser.Plugins.Anime.Providers;
using MediaBrowser.Plugins.Anime.Providers.AniList;
using Moq;
using NUnit.Framework;

namespace MediaBrowser.Plugins.Anime.Tests
{
    [TestFixture]
    public class AniListSeriesProviderTests
    {
        [Test]
        public async Task TestScrapePage()
        {
            var downloader = new Mock<IAniListDownloader>();
            downloader.Setup(d => d.DownloadSeriesPage(It.IsAny<string>())).Returns(Task.FromResult(new FileInfo("TestData/anilist/9756.html")));

            var logger = new Mock<ILogger>();

            var paths = new Mock<IServerApplicationPaths>();
            paths.Setup(p => p.DataPath).Returns("TestData");

            var providerIds = new Dictionary<string, string>
            {
                { ProviderNames.MyAnimeList, "9756" }
            };

            var config = new PluginConfiguration
            {
                TitlePreference = TitlePreferenceType.JapaneseRomaji
            };

            PluginConfiguration.Instance = () => config;

            var anilist = new AniListSeriesProvider(downloader.Object, logger.Object);
            SeriesInfo info = await anilist.FindSeriesInfo(providerIds, "en", CancellationToken.None);

            Assert.That(info.Name, Is.EqualTo("Mahou Shoujo Madoka★Magica"));
            Assert.That(info.Genres, Contains.Item("Drama"));
            Assert.That(info.Genres, Contains.Item("Magic"));
            Assert.That(info.Genres, Contains.Item("Psychological"));
            Assert.That(info.Genres, Contains.Item("Thriller"));
            Assert.That(info.StartDate, Is.EqualTo(new DateTime(2011, 1, 7)));
            Assert.That(info.EndDate, Is.EqualTo(new DateTime(2011, 4, 22)));
        }
    }
}