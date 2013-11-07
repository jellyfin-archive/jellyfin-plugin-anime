using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Serialization;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugins.Anime.Providers.MyAnimeList;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    public class AniDbPersonImageProvider : BaseMetadataProvider
    {
        private const string PersonUrl = @"http://anidb.net/perl-bin/animedb.pl?show=creator&creatorid={0}";

        private static readonly Regex DescriptionRegex = new Regex(@"<div class=""g_bubble desc"">\s*(?<description>.*?)\s*</div>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex ImageRegex = new Regex(@"<div class=""image"">\s*<img src=""(?<image>http://img7\.anidb\.net/pics/anime/\d+\.jpg)"" alt="".*?"" />\s*</div>", RegexOptions.Singleline | RegexOptions.Compiled);
        private readonly IHttpClient _httpClient;

        private readonly ILibraryManager _library;
        private readonly IProviderManager _providerManager;

        private static readonly RateLimiter HttpRateLimiter = new RateLimiter(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(15));

        public AniDbPersonImageProvider(ILogManager logManager, IServerConfigurationManager configurationManager, ILibraryManager library, IProviderManager providerManager, IHttpClient httpClient)
            : base(logManager, configurationManager)
        {
            _library = library;
            _providerManager = providerManager;
            _httpClient = httpClient;
        }

        protected override bool RefreshOnVersionChange
        {
            get { return true; }
        }

        protected override string ProviderVersion
        {
            get { return "1"; }
        }

        public override bool RequiresInternet
        {
            get { return true; }
        }

        // lie for the moment
        public override bool IsSlow
        {
            get { return false; }
        }

        public override MetadataProviderPriority Priority
        {
            get { return MetadataProviderPriority.Fourth; }
        }

        public override ItemUpdateType ItemUpdateType
        {
            get { return ItemUpdateType.MetadataDownload | ItemUpdateType.ImageUpdate; }
        }

        public override bool Supports(BaseItem item)
        {
            return item is Person;
        }

        public override async Task<bool> FetchAsync(BaseItem item, bool force, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(item.PrimaryImagePath) || string.IsNullOrEmpty(item.Overview))
            {
                List<Series> seriesWithPerson = _library.RootFolder
                                                        .RecursiveChildren
                                                        .OfType<Series>()
                                                        .Where(i => !string.IsNullOrEmpty(i.GetProviderId(ProviderNames.AniDb)) && i.People.Any(p => string.Equals(p.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
                                                        .ToList();

                string peoplePath = Path.Combine(ConfigurationManager.ApplicationPaths.DataPath, "anidb", "people");

                foreach (Series series in seriesWithPerson)
                {
                    try
                    {
                        string seriesPath = AniDbSeriesProvider.GetSeriesDataPath(ConfigurationManager.ApplicationPaths, series.GetProviderId(ProviderNames.AniDb));
                        AniDbPersonInfo person = TryFindPerson(item.Name, seriesPath);
                        if (person != null)
                        {
                            if (!string.IsNullOrEmpty(person.Image))
                            {
                                await _providerManager.SaveImage(item, person.Image, AniDbSeriesProvider.ResourcePool, ImageType.Primary, null, cancellationToken)
                                                      .ConfigureAwait(false);
                            }

                            //await FindPersonInfo(item, person, peoplePath, cancellationToken).ConfigureAwait(false);
                            break;
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        // No biggie
                    }
                }
            }

            SetLastRefreshed(item, DateTime.UtcNow);
            return true;
        }

        private AniDbPersonInfo TryFindPerson(string name, string dataPath)
        {
            string castXml = Path.Combine(dataPath, "cast.xml");

            if (string.IsNullOrEmpty(castXml) || !File.Exists(castXml))
            {
                return null;
            }

            var serializer = new XmlSerializer(typeof (CastList));
            using (FileStream stream = File.Open(castXml, FileMode.Open, FileAccess.Read))
            {
                var list = (CastList) serializer.Deserialize(stream);
                return list.Cast.FirstOrDefault(p => string.Equals(name, AniDbSeriesProvider.ReverseNameOrder(p.Name), StringComparison.OrdinalIgnoreCase));
            }
        }

        private async Task FindPersonInfo(BaseItem item, AniDbPersonInfo person, string dataPath, CancellationToken cancellationToken)
        {
            string cachedPath = Path.Combine(dataPath, person.Name.First().ToString(CultureInfo.InvariantCulture), person.Name + ".html");
            var cached = new FileInfo(cachedPath);

            if (!cached.Exists)
            {
                string url = string.Format(PersonUrl, person.Id);

                try
                {
                    string directory = Path.GetDirectoryName(cachedPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var requestOptions = new HttpRequestOptions
                    {
                        Url = url,
                        CancellationToken = cancellationToken,
                        EnableHttpCompression = false,
                        UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/30.0.1599.101 Safari/537.36"
                    };

                    await HttpRateLimiter.Tick().ConfigureAwait(false);

                    using (Stream stream = await _httpClient.Get(requestOptions).ConfigureAwait(false))
                    using (var unzipped = new GZipStream(stream, CompressionMode.Decompress))
                    using (FileStream file = File.Open(cachedPath, FileMode.Create, FileAccess.Write))
                    {
                        await unzipped.CopyToAsync(file).ConfigureAwait(false);
                    }

                    cached = new FileInfo(cachedPath);
                }
                catch (Exception e)
                {
                    Logger.ErrorException("Failed to download {0}", e, url);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            await ScrapePage((Person) item, cached, cancellationToken).ConfigureAwait(false);
        }

        private async Task ScrapePage(Person person, FileInfo file, CancellationToken cancellationToken)
        {
            string data = File.ReadAllText(file.FullName);
            ScrapeDescription(person, data);
            await ScrapeImage(person, data, cancellationToken).ConfigureAwait(false);
        }

        private void ScrapeDescription(Person person, string data)
        {
            Match match = DescriptionRegex.Match(data);
            if (match.Success)
            {
                string description = HttpUtility.HtmlDecode(match.Groups["description"].Value);
                description = description.Replace("<br>", " \n");

                person.Overview = MalSeriesProvider.StripHtml(description);
            }
        }

        private async Task ScrapeImage(Person person, string data, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(person.PrimaryImagePath))
            {
                return;
            }

            Match match = ImageRegex.Match(data);
            if (match.Success)
            {
                string url = match.Groups["image"].Value;
                if (!string.IsNullOrEmpty(url))
                {
                    await _providerManager.SaveImage(person, url, AniDbSeriesProvider.ResourcePool, ImageType.Primary, null, cancellationToken)
                                          .ConfigureAwait(false);
                }
            }
        }
    }
}