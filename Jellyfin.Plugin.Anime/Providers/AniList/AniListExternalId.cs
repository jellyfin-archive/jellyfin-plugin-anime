using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Anime.Providers.AniList
{
    public class AniListExternalId : IExternalId
    {
        public bool Supports(IHasProviderIds item)
            => item is Series;

        public string Name
            => "AniList";

        public string Key
            => ProviderNames.AniList;

        public string UrlFormatString
            => "http://anilist.co/anime/{0}/";
    }
}
