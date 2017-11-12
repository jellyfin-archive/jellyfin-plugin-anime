using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Plugins.Anime.Providers.AniSearch
{
    public class AniSearchExternalId : IExternalId
    {
        public bool Supports(IHasProviderIds item)
        {
            return item is Series;
        }

        public string Name
        {
            get { return "AniSearch"; }
        }

        public string Key
        {
            get { return ProviderNames.AniSearch; }
        }

        public string UrlFormatString
        {
            get { return "http://www.anisearch.de/anime/{0}"; }
        }
    }
}