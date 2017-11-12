using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Plugins.Anime.Providers.Proxer
{
    public class ProxerExternalId : IExternalId
    {
        public bool Supports(IHasProviderIds item)
        {
            return item is Series;
        }

        public string Name
        {
            get { return "Proxer"; }
        }

        public string Key
        {
            get { return ProviderNames.Proxer; }
        }

        public string UrlFormatString
        {
            get { return "https://proxer.me/info/{0}"; }
        }
    }
}