using System;
using System.Collections.Generic;
using MediaBrowser.Controller.Entities;

namespace MediaBrowser.Plugins.Anime.Providers
{
    public class EpisodeInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime? AirDate { get; set; }
        public float? CommunityRating { get; set; }
        public int? VoteCount { get; set; }
        public long? RunTimeTicks { get; set; }
        public List<PersonInfo> People { get; set; }
        public List<string> Genres { get; set; }
        public List<string> Studios { get; set; }
        public Dictionary<string, string> ExternalProviders { get; set; }

        public EpisodeInfo()
        {
            People = new List<PersonInfo>();
            Genres = new List<string>();
            Studios = new List<string>();
            ExternalProviders = new Dictionary<string, string>();
        }
    }
}
