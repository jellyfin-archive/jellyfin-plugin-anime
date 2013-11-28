using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugins.Anime.Configuration;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    /// <summary>
    ///     Copies the series image into a season, if the season does not otherwise have any primary image.
    /// </summary>
    public class AniDbSeasonImageProvider : BaseMetadataProvider
    {
        private readonly IProviderManager _providerManager;

        public AniDbSeasonImageProvider(ILogManager logManager, IServerConfigurationManager configurationManager, IProviderManager providerManager)
            : base(logManager, configurationManager)
        {
            _providerManager = providerManager;
        }

        public override MetadataProviderPriority Priority
        {
            get { return MetadataProviderPriority.First; }
        }

        public override bool Supports(BaseItem item)
        {
            return item is Season;
        }

        protected override bool NeedsRefreshInternal(BaseItem item, BaseProviderInfo providerInfo)
        {
            if (item.HasImage(ImageType.Primary))
            {
                return false;
            }

            return base.NeedsRefreshInternal(item, providerInfo);
        }

        public override async Task<bool> FetchAsync(BaseItem item, bool force, CancellationToken cancellationToken)
        {
            var season = (Season)item;
            string seriesId = season.Series.GetProviderId(ProviderNames.AniDb);

            if (!string.IsNullOrEmpty(seriesId) && !season.HasImage(ImageType.Primary))
            {
                var seriesImage = season.Series.GetImagePath(ImageType.Primary, 0);
                if (!string.IsNullOrEmpty(seriesImage))
                {
                    _providerManager.SaveImage(season, seriesImage, AniDbSeriesProvider.ResourcePool, ImageType.Primary, null, cancellationToken)
                                    .ConfigureAwait(false);
                }
            }

            SetLastRefreshed(item, DateTime.Now);
            return true;
        }
    }
}