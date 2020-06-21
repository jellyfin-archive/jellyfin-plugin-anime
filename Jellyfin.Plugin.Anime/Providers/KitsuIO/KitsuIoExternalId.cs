using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Anime.Providers.KitsuIO
{
    public class KitsuIoExternalId : IExternalId
    {
        public bool Supports(IHasProviderIds item)
            => item is MediaBrowser.Controller.Entities.TV.Series || item is Movie;
        
        public string Name
            => "KitsuIO";
        
        public string Key
            => ProviderNames.KitsuIo;

        public string UrlFormatString
            => "https://kitsu.io/anime/{0}";
    }
}