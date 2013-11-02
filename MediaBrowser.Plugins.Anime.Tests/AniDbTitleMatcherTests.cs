using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugins.Anime.Providers.AniDB;
using Moq;
using NUnit.Framework;

namespace MediaBrowser.Plugins.Anime.Tests
{
    [TestFixture]
    public class AniDbTitleMatcherTests
    {
        [Test]
        public async Task LoadsOnFindIfNotLoaded()
        {
            var logger = new Mock<ILogger>();

            var downloader = new Mock<IAniDbTitleDownloader>();
            downloader.Setup(d => d.Load(It.IsAny<CancellationToken>())).Returns(Task.FromResult(0));
            downloader.Setup(d => d.TitlesFilePath).Returns("TestData/anidb/titles.xml");

            var matcher = new AniDbTitleMatcher(logger.Object, downloader.Object);

            Assert.That(matcher.IsLoaded, Is.False);

            Assert.That(await matcher.FindSeries("CotS"), Is.EqualTo("1"));
            Assert.That(await matcher.FindSeries("Crest of the Stars"), Is.EqualTo("1"));
            Assert.That(await matcher.FindSeries("星界の紋章"), Is.EqualTo("1"));
            Assert.That(await matcher.FindSeries("サザンアイズ"), Is.EqualTo("2"));
            Assert.That(await matcher.FindSeries("3x3 Eyes"), Is.EqualTo("2"));
            Assert.That(await matcher.FindSeries("Sazan Eyes"), Is.EqualTo("2"));
        }

        [Test]
        public async Task ErrorLoggedIfTitlesFileMissing()
        {
            var downloader = new Mock<IAniDbTitleDownloader>();
            downloader.Setup(d => d.Load(It.IsAny<CancellationToken>())).Returns(Task.FromResult(0));
            downloader.Setup(d => d.TitlesFilePath).Returns("InvalidPath");

            var logger = new Mock<ILogger>();
            logger.Setup(l => l.ErrorException("Failed to load AniDB titles", It.IsAny<Exception>())).Verifiable();

            var matcher = new AniDbTitleMatcher(logger.Object, downloader.Object);

            await matcher.FindSeries("CotS");

            logger.Verify();
        }
    }
}
