using System;
using System.Collections.Generic;
using MediaBrowser.Controller.Entities;

namespace MediaBrowser.Plugins.AniDB.Providers
{
    public class SeriesInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public float? CommunityRating { get; set; }
        public int? VoteCount { get; set; }
        public List<PersonInfo> People { get; set; }
        public List<DayOfWeek> AirDays { get; set; }
        public string AirTime { get; set; }
        public string ContentRating { get; set; }
        public long? RunTimeTicks { get; set; }
        public List<string> Genres { get; set; }
        public List<string> Studios { get; set; }
        public Dictionary<string, string> ExternalProviders { get; set; }

        public SeriesInfo()
        {
            People = new List<PersonInfo>();
            AirDays = new List<DayOfWeek>();
            Studios = new List<string>();
            ExternalProviders = new Dictionary<string, string>();
            Genres = new List<string>();
        }
    }
}
