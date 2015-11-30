using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Plugins.Anime.Configuration;
using MediaBrowser.Plugins.Anime.Providers.AniDB.Converter;
using MediaBrowser.Plugins.Anime.Providers.AniDB.Identity;

namespace MediaBrowser.Plugins.Anime
{
    public class Plugin
        : BasePlugin<PluginConfiguration>
    {
        private readonly IApplicationPaths _applicationPaths;

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger logger) : base(applicationPaths, xmlSerializer)
        {
            _applicationPaths = applicationPaths;
            AniDbTitleMatcher.DefaultInstance = new AniDbTitleMatcher(logger, new AniDbTitleDownloader(logger, applicationPaths));
            AnidbConverter.DefaultInstance = new AnidbConverter(applicationPaths);

            Instance = this;
            PluginConfiguration.Instance = () => Configuration;

            PerformMigrations();
        }

        public override string Name
        {
            get { return "Anime"; }
        }

        public static Plugin Instance { get; private set; }

        private void PerformMigrations()
        {
            var previousVersion = ReadSavedVersion();
            var currentVersion = Version.Parse(GitVersionInformation.AssemblySemVer);
            
            try
            {
                if (previousVersion == null)
                    InitialSetup(currentVersion);
                else
                    Migrate(previousVersion, currentVersion);
            }
            finally
            {
                var path = Path.Combine(_applicationPaths.CachePath, "anime", "version.txt");
                var dir = Path.GetDirectoryName(path);
                Directory.CreateDirectory(dir);
                File.WriteAllText(path, currentVersion.ToString());
            }
        }

        private void Migrate(Version previousVersion, Version currentVersion)
        {
        }

        private void InitialSetup(Version currentVersion)
        {
            // pre-2.0.0 had no version checks, and so upgrades to 2.0.0+ will be detected as needing initial setup
            // default to UseAnidbOrderingWithSeasons = true if there was a previous install, to emulate pre-2.0.0 behaviour 
            if (Directory.Exists(Path.Combine(_applicationPaths.CachePath, "anidb")))
            {
                Configuration.UseAnidbOrderingWithSeasons = true;
                SaveConfiguration();
            }

            // force refresh on new installs
            SetForceRefreshFlag();
        }

        private Version ReadSavedVersion()
        {
            var path = Path.Combine(_applicationPaths.CachePath, "anime", "version.txt");
            if (!File.Exists(path))
                return null;

            try
            {
                var version = File.ReadAllText(path);
                return Version.Parse(version);
            }
            catch
            {
                return null;
            }
        }

        public void SetForceRefreshFlag()
        {
            try
            {
                using (File.Create(ForceRefreshFlagPath))
                {
                }
            }
            catch { }
        }

        public void ClearForceRefreshFlag()
        {
            try
            {
                File.Delete(ForceRefreshFlagPath);
            }
            catch { }
        }

        public bool CheckForceRefreshFlag()
        {
            return File.Exists(ForceRefreshFlagPath);
        }

        private string ForceRefreshFlagPath => Path.Combine(_applicationPaths.CachePath, "anime", ".forcerefresh");
    }

    public class ClearRefreshFlagPostScanTask : ILibraryPostScanTask, IHasOrder
    {
        public Task Run(IProgress<double> progress, CancellationToken cancellationToken)
        {
            Plugin.Instance.ClearForceRefreshFlag();
            return Task.FromResult<object>(null);
        }

        public int Order => int.MaxValue;
    }
}
