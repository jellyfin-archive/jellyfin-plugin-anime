using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB
{
    public class AniDbExternalId : IExternalId
    {
        public bool Supports(IHasProviderIds item)
        {
            return item is Series;
        }

        public string Name
        {
            get { return "AniDB"; }
        }

        public string Key
        {
            get { return ProviderNames.AniDb; }
        }

        public string UrlFormatString
        {
            get { return "http://anidb.net/perl-bin/animedb.pl?show=anime&aid={0}"; }
        }
    }
}