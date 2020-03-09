using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Anime.Providers.AniDB
{
    public class AniDbExternalId : IExternalId
    {
        public bool Supports(IHasProviderIds item)
            => item is Series || item is Movie;

        public string Name
            => "AniDB";

        public string Key
            => ProviderNames.AniDb;

        public string UrlFormatString
            => "http://anidb.net/perl-bin/animedb.pl?show=anime&aid={0}";
    }
}
