using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.ScheduledTasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugins.Anime.Configuration;
using MediaBrowser.Plugins.Anime.Providers.MyAnimeList;
using MoreLinq;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    public class AniDbPersonInfoTask : IScheduledTask
    {
        private const string PersonUrl = @"http://anidb.net/perl-bin/animedb.pl?show=creator&creatorid={0}";

        private static readonly Regex DescriptionRegex = new Regex(@"<div class=""g_bubble desc"">\s*(?<description>.*?)\s*</div>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex ImageRegex = new Regex(@"<div class=""image"">\s*<img src=""(?<image>http://img7\.anidb\.net/pics/anime/\d+\.jpg)"" alt="".*?"" />\s*</div>", RegexOptions.Singleline | RegexOptions.Compiled);
        private readonly IServerConfigurationManager _configuration;

        private readonly IHttpClient _httpClient;
        private readonly ILibraryManager _library;
        private readonly ILogger _logger;
        private readonly IProviderManager _providerManager;

        private readonly IntervalTrigger _repeatTrigger;

        public AniDbPersonInfoTask(IHttpClient httpClient, ILibraryManager library, IProviderManager providerManager, ILogManager logManager, IServerConfigurationManager configuration)
        {
            _httpClient = httpClient;
            _library = library;
            _providerManager = providerManager;
            _configuration = configuration;
            _logger = logManager.GetLogger(typeof (AniDbPersonInfoTask).Name);
            _repeatTrigger = new IntervalTrigger {Interval = TimeSpan.FromMinutes(20)};

            EventHandler<ItemChangeEventArgs> itemChanged = (sender, arg) =>
            {
                var person = arg.Item as Person;
                if (person != null && RequiresUpdate(person))
                {
                    _repeatTrigger.Start(false);
                }
            };

            _library.ItemAdded += itemChanged;
            _library.ItemUpdated += itemChanged;
        }

        private bool RequiresUpdate(Person person)
        {
            return !string.IsNullOrEmpty(person.GetProviderId(ProviderNames.AniDb)) &&
                   (string.IsNullOrEmpty(person.PrimaryImagePath) || string.IsNullOrEmpty(person.Overview));
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var personInfo = _library.RootFolder
                                     .GetRecursiveChildren()
                                     .SelectMany(c => c.People)
                                     .DistinctBy(p => p.Name, StringComparer.OrdinalIgnoreCase);
            
            List<Person> people = personInfo.Select(p => _library.GetPerson(p.Name)).ToList();
            people = people.Where(RequiresUpdate).ToList();

            int i;
            for (i = 0; i < people.Count; i++)
            {
                Person person = people[i];

                _logger.Debug("Searching for AniDB data for {0}.", person.Name);

                if (await FindPersonInfo(person, cancellationToken))
                {
                    break;
                }

                progress.Report(100*((double) (i + 1)/people.Count));
            }

            progress.Report(100);

            if (i < people.Count - 1)
            {
                _repeatTrigger.Start(false);
            }
        }

        public IEnumerable<ITaskTrigger> GetDefaultTriggers()
        {
            return new ITaskTrigger[]
            {
                _repeatTrigger
            };
        }

        public string Name
        {
            get { return "AniDB Person Data Download"; }
        }

        public string Description
        {
            get { return "Downloads additional person data from AniDB."; }
        }

        public string Category
        {
            get { return "Library"; }
        }

        private async Task<bool> FindPersonInfo(BaseItem item, CancellationToken cancellationToken)
        {
            string id = item.GetProviderId(ProviderNames.AniDb);
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            string cachedPath = Path.Combine(_configuration.ApplicationPaths.DataPath, "anidb", "people", id.First().ToString(CultureInfo.InvariantCulture), id + ".html");
            var cached = new FileInfo(cachedPath);

            bool dataRequested = false;

            if (!cached.Exists || (PluginConfiguration.Instance.AllowAutomaticMetadataUpdates && DateTime.Now - cached.LastWriteTime > TimeSpan.FromDays(30)))
            {
                string url = string.Format(PersonUrl, id);

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

                    using (Stream stream = await _httpClient.Get(requestOptions).ConfigureAwait(false))
                    using (var unzipped = new GZipStream(stream, CompressionMode.Decompress))
                    using (FileStream file = File.Open(cachedPath, FileMode.Create, FileAccess.Write))
                    {
                        await unzipped.CopyToAsync(file).ConfigureAwait(false);
                    }

                    cached = new FileInfo(cachedPath);
                    dataRequested = true;
                }
                catch (Exception e)
                {
                    _logger.ErrorException("Failed to download {0}", e, url);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (await ScrapePage((Person) item, cached, cancellationToken).ConfigureAwait(false))
            {
                await _library.UpdateItem(item, ItemUpdateType.ImageUpdate, cancellationToken);
            }

            return dataRequested;
        }

        private async Task<bool> ScrapePage(Person person, FileInfo file, CancellationToken cancellationToken)
        {
            string data = File.ReadAllText(file.FullName);
            bool descriptionFound = ScrapeDescription(person, data);
            bool imageFound = await ScrapeImage(person, data, cancellationToken).ConfigureAwait(false);

            return descriptionFound || imageFound;
        }

        private bool ScrapeDescription(Person person, string data)
        {
            Match match = DescriptionRegex.Match(data);
            if (match.Success)
            {
                string description = HttpUtility.HtmlDecode(match.Groups["description"].Value);
                description = description.Replace("<br>", " \n");
                description = MalSeriesProvider.StripHtml(description);

                if (person.Overview != description)
                {
                    person.Overview = description;
                    return true;
                }
            }

            return false;
        }

        private async Task<bool> ScrapeImage(Person person, string data, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(person.PrimaryImagePath))
            {
                return false;
            }

            Match match = ImageRegex.Match(data);
            if (match.Success)
            {
                string url = match.Groups["image"].Value;
                if (!string.IsNullOrEmpty(url))
                {
                    await _providerManager.SaveImage(person, url, AniDbSeriesProvider.ResourcePool, ImageType.Primary, null, cancellationToken)
                                          .ConfigureAwait(false);

                    return true;
                }
            }

            return false;
        }
    }
}