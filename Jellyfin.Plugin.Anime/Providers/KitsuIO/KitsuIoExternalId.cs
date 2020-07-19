using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Anime.Providers.KitsuIO
{
    public class KitsuIoExternalId : IExternalId
    {
        public bool Supports(IHasProviderIds item)
            => item is Series || item is Movie;
        
        public string ProviderName
            => "Kitsu";
        
        public string Key
            => ProviderNames.KitsuIo;

        public ExternalIdMediaType? Type 
            => ExternalIdMediaType.Series;

        public string UrlFormatString
            => "https://kitsu.io/anime/{0}";
    }
}
