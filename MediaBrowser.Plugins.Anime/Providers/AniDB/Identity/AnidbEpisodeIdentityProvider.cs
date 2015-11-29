using System.Threading.Tasks;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Plugins.Anime.Providers.AniDB.Converter;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB.Identity
{
    public class AnidbEpisodeIdentityProvider : IItemIdentityProvider<EpisodeInfo>
    {
        public async Task Identify(EpisodeInfo info)
        {
            if (info.ProviderIds.ContainsKey(ProviderNames.AniDb))
                return;

            var series = info.SeriesProviderIds.GetOrDefault(ProviderNames.AniDb);
            if (!string.IsNullOrEmpty(series) && info.ParentIndexNumber == null && info.IndexNumber != null)
            {
                var id = new AnidbEpisodeIdentity(series, info.IndexNumber.Value, info.IndexNumberEnd, null);
                info.ProviderIds.Add(ProviderNames.AniDb, id.ToString());
            }
        }
    }
}