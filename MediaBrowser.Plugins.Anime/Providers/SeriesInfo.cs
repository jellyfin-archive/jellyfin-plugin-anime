using System;
using System.Collections.Generic;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Plugins.Anime.Providers
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
        public List<string> Tags { get; set; }
        public List<string> Studios { get; set; }
        public Dictionary<string, string> ExternalProviders { get; set; }

        public SeriesInfo()
        {
            People = new List<PersonInfo>();
            AirDays = new List<DayOfWeek>();
            Studios = new List<string>();
            ExternalProviders = new Dictionary<string, string>();
            Genres = new List<string>();
            Tags = new List<string>();
        }

        public static SeriesInfo FromSeries(Series series)
        {
            return new SeriesInfo
            {
                Name = series.Name,
                Description = series.Overview,
                StartDate = series.PremiereDate,
                EndDate = series.DateLastEpisodeAdded,
                CommunityRating = series.CommunityRating,
                VoteCount = series.VoteCount,
                People = series.People,
                AirDays = series.AirDays,
                AirTime = series.AirTime,
                ContentRating = series.OfficialRating,
                RunTimeTicks = series.RunTimeTicks,
                Genres = series.Genres,
                Tags = series.Tags,
                Studios = series.Studios,
                ExternalProviders = series.ProviderIds
            };
        }

        public void Set(Series series)
        {
            if (!series.LockedFields.Contains(MetadataFields.Name))
                series.Name = Name;

            if (!series.LockedFields.Contains(MetadataFields.Overview))
                series.Overview = Description;

            if (!series.LockedFields.Contains(MetadataFields.Cast))
                series.People = People;

            if (!series.LockedFields.Contains(MetadataFields.OfficialRating))
                series.OfficialRating = ContentRating;

            if (!series.LockedFields.Contains(MetadataFields.Runtime))
                series.RunTimeTicks = RunTimeTicks;

            if (!series.LockedFields.Contains(MetadataFields.Genres))
                series.Genres = Genres;

            if (!series.LockedFields.Contains(MetadataFields.Tags))
                series.Tags = Tags;

            if (!series.LockedFields.Contains(MetadataFields.Studios))
                series.Studios = Studios;

            foreach (var provider in ExternalProviders)
                series.ProviderIds[provider.Key] = provider.Value;
            
            series.PremiereDate = StartDate;
            series.EndDate = EndDate;
            series.Status = EndDate != null ? SeriesStatus.Ended : SeriesStatus.Continuing;
            series.CommunityRating = CommunityRating;
            series.VoteCount = VoteCount;
            series.AirDays = AirDays;
            series.AirTime = AirTime;

            if (series.ProductionYear == null && series.PremiereDate != null)
                series.ProductionYear = series.PremiereDate.Value.Year;
        }
    }
}
