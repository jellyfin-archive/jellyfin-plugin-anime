using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Anime.Providers.AniSearch
{
    public class AniSearchExternalId : IExternalId
    {
        public bool Supports(IHasProviderIds item)
            => item is Series;

        public string ProviderName
            => "AniSearch";

        public string Key
            => ProviderNames.AniSearch;

        public ExternalIdMediaType? Type
            => ExternalIdMediaType.Series;

        public string UrlFormatString
            => "https://www.anisearch.com/anime/{0}";
    }
}
