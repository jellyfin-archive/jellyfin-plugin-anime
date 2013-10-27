using System;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Logging;
using Moq;
using NUnit.Framework;

namespace MediaBrowser.Plugins.AniDB.Tests
{
    [TestFixture]
    public class AniDbTitleMatcherTests
    {
        [Test]
        public async Task LoadsTitles()
        {
            var paths = new Mock<IApplicationPaths>();
            paths.Setup(p => p.DataPath).Returns("TestData");

            var logger = new Mock<ILogger>();

            var matcher = new AniDbTitleMatcher(paths.Object, logger.Object);
            await matcher.Load();

            Assert.That(matcher.IsLoaded, Is.True);

            Assert.That(await matcher.FindSeries("CotS"), Is.EqualTo("1"));
            Assert.That(await matcher.FindSeries("Crest of the Stars"), Is.EqualTo("1"));
            Assert.That(await matcher.FindSeries("星界の紋章"), Is.EqualTo("1"));
            Assert.That(await matcher.FindSeries("サザンアイズ"), Is.EqualTo("2"));
            Assert.That(await matcher.FindSeries("3x3 Eyes"), Is.EqualTo("2"));
            Assert.That(await matcher.FindSeries("Sazan Eyes"), Is.EqualTo("2"));
        }

        [Test]
        public async Task LoadsOnFindIfNotLoaded()
        {
            var paths = new Mock<IApplicationPaths>();
            paths.Setup(p => p.DataPath).Returns("TestData");

            var logger = new Mock<ILogger>();

            var matcher = new AniDbTitleMatcher(paths.Object, logger.Object);

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
            var paths = new Mock<IApplicationPaths>();
            paths.Setup(p => p.DataPath).Returns("InvalidPath");

            var logger = new Mock<ILogger>();
            logger.Setup(l => l.ErrorException("Failed to load AniDB titles", It.IsAny<Exception>())).Verifiable();

            var matcher = new AniDbTitleMatcher(paths.Object, logger.Object);
            await matcher.Load();

            logger.Verify();
        }
    }
}
