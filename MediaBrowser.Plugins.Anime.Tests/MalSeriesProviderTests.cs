using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugins.Anime.Providers;
using MediaBrowser.Plugins.Anime.Providers.MyAnimeList;
using Moq;
using NUnit.Framework;

namespace MediaBrowser.Plugins.Anime.Tests
{
    [TestFixture]
    public class MalSeriesProviderTests
    {
        [Test]
        public async Task TestScrapePage()
        {
            var downloader = new Mock<IMalDownloader>();
            downloader.Setup(d => d.DownloadSeriesPage(It.IsAny<string>())).Returns(Task.FromResult(File.ReadAllText("TestData/mal/9756.html")));

            var logger = new Mock<ILogger>();

            var providerIds = new Dictionary<string, string>
            {
                { ProviderNames.MyAnimeList, "9756" }
            };

            var mal = new MalSeriesProvider(downloader.Object, logger.Object);
            var info = await mal.FindSeriesInfo(providerIds, "en", CancellationToken.None);

            Assert.That(info.Name, Is.EqualTo("Mahou Shoujo Madoka★Magica"));
            Assert.That(info.Genres, Contains.Item("Drama"));
            Assert.That(info.Genres, Contains.Item("Magic"));
            Assert.That(info.Genres, Contains.Item("Psychological"));
            Assert.That(info.Genres, Contains.Item("Thriller"));
            Assert.That(info.StartDate, Is.EqualTo(new DateTime(2011, 1, 7)));
            Assert.That(info.EndDate, Is.EqualTo(new DateTime(2011, 4, 22)));
            Assert.That(info.CommunityRating, Is.EqualTo(8.68f));
            Assert.That(info.VoteCount, Is.EqualTo(103201));
        }
    }
}
