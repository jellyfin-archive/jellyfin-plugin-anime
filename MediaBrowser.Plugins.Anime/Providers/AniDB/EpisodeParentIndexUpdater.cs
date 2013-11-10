using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Logging;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    public class EpisodeParentIndexUpdater
        : BaseMetadataProvider
    {
        public EpisodeParentIndexUpdater(ILogManager logManager, IServerConfigurationManager configurationManager) : base(logManager, configurationManager)
        {
        }

        public override MetadataProviderPriority Priority
        {
            get { return MetadataProviderPriority.First; }
        }

        public override bool Supports(BaseItem item)
        {
            return item is Episode;
        }

        public override Task<bool> FetchAsync(BaseItem item, bool force, CancellationToken cancellationToken)
        {
            var episode = (Episode) item;
            if (episode.Season != null && episode.Season.IndexNumber != null)
            {
                episode.ParentIndexNumber = episode.Season.IndexNumber;
            }

            SetLastRefreshed(item, DateTime.Now);
            return Task.FromResult(true);
        }
    }
}