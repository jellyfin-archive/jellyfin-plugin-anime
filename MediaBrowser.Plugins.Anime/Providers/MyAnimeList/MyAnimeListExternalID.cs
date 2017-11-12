using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Plugins.Anime.Providers.MyAnimeList
{
    public class MyAnimeListrExternalId : IExternalId
    {
        public bool Supports(IHasProviderIds item)
        {
            return item is Series;
        }

        public string Name
        {
            get { return "MyAnimeList"; }
        }

        public string Key
        {
            get { return ProviderNames.MyAnimeList; }
        }

        public string UrlFormatString
        {
            get { return "https://myanimelist.net/anime/{0}"; }
        }
    }
}