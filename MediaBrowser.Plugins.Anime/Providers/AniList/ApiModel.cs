using System;
using System.Collections.Generic;

namespace MediaBrowser.Plugins.Anime.Providers.AniList
{
    public class Anime
    {
        public int id { get; set; }
        public string description { get; set; }
        public string title_romaji { get; set; }
        public string title_japanese { get; set; }
        public string title_english { get; set; }
        public string start_date { get; set; }
        public string end_date { get; set; }
        public string image_url_lge { get; set; }
        public string image_url_banner { get; set; }
        public List<string> genres { get; set; }
        public int? duration { get; set; }
        public string airing_status { get; set; }
    }
}