using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Plugins.Anime.Configuration;

namespace MediaBrowser.Plugins.Anime
{
    public static class SeriesFilter
    {
        public static bool SeriesIsIgnored(Series series, ILibraryManager library)
        {
            PluginConfiguration config = PluginConfiguration.Instance();

            List<string> ignoredVirtualFolderLocations = library.GetDefaultVirtualFolders().Where(vf => config.IgnoredVirtualFolders.Contains(vf.Name))
                                                                .SelectMany(vf => vf.Locations)
                                                                .ToList();

            for (Folder item = series.Parent; item != null; item = item.Parent)
            {
                if (!item.IsFolder)
                    continue;

                if (config.IgnoredPhysicalLocations.Contains(item.Path))
                {
                    return true;
                }

                if (ignoredVirtualFolderLocations.Contains(item.Path))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
