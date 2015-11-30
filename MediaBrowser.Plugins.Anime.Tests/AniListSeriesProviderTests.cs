using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
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
//            var data = File.ReadAllText("TestData/anilist/9756.html", Encoding.UTF8);
//
//            var series = new Series();
//
//            AniListSeriesProvider.ParseTitle(series, data, "en");
//            AniListSeriesProvider.ParseSummary(series, data);
//            AniListSeriesProvider.ParseStudio(series, data);
//            AniListSeriesProvider.ParseRating(series, data);
//            AniListSeriesProvider.ParseGenres(series, data);
//            AniListSeriesProvider.ParseDuration(series, data);
//            AniListSeriesProvider.ParseAirDates(series, data);
//
//            Assert.That(series.Name, Is.EqualTo("Mahou Shoujo Madoka★Magica"));
//            Assert.That(series.Genres, Contains.Item("Drama"));
//            Assert.That(series.Genres, Contains.Item("Fantasy"));
//            Assert.That(series.Genres, Contains.Item("Psychological Thriller"));
//            Assert.That(series.Genres, Contains.Item("Thriller"));
//            Assert.That(series.PremiereDate, Is.EqualTo(new DateTime(2011, 1, 7)));
//            Assert.That(series.EndDate, Is.EqualTo(new DateTime(2011, 4, 22)));

        }
    }
}