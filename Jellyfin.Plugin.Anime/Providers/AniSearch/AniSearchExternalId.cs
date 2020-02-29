using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Anime.Providers.AniSearch
{
    public class AniSearchExternalId : IExternalId
    {
        public bool Supports(IHasProviderIds item)
            => item is Series;

        public string Name
            => "AniSearch";

        public string Key
            => ProviderNames.AniSearch;

        public string UrlFormatString
            => "http://www.anisearch.com/anime/{0}";
    }
}
