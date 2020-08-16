using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Anime.Providers.KitsuIO.ApiClient
{
    public class ApiListResponse
    {
        public List<Series> Data { get; set; }
        public ResponseMeta Meta { get; set; }
    }

    public class ApiResponse
    {
        public Series Data { get; set; }
        public List<Included> Included { get; set; }
    }

    public class Series
    {
        public long Id { get; set; }
        public Attributes Attributes { get; set; }
    }

    public class Attributes
    {
        public string Synopsis { get; set; }
        public Titles Titles { get; set; }
        public string AverageRating { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public PosterImage PosterImage { get; set; }

        // Episode specific
        public int? Number { get; set; }
        public int? SeasonNumber { get; set; }
        public DateTime? AirDate { get; set; }
        public int? Length { get; set; }
    }

    public class PosterImage
    {
        public Uri Medium { get; set; }
        public Uri Original { get; set; }
    }

    public class Titles
    {
        [JsonPropertyName("en")] public string En { get; set; }

        [JsonPropertyName("en_jp")] public string EnJp { get; set; }

        [JsonPropertyName("ja_jp")] public string JaJp { get; set; }

        [JsonPropertyName("en_us")] public string EnUs { get; set; }

        public string GetTitle =>
            !string.IsNullOrWhiteSpace(En) ? En :
            !string.IsNullOrWhiteSpace(EnUs) ? EnUs :
            !string.IsNullOrWhiteSpace(EnJp) ? EnJp :
            JaJp;

        public bool Equal(string title)
        {
            return
                (En?.Equals(title) ?? false) ||
                (EnUs?.Equals(title) ?? false) ||
                (EnJp?.Equals(title) ?? false) ||
                (JaJp?.Equals(title) ?? false);
        }
    }
    
    public class ResponseMeta
    {
        public long? Count { get; set; }
    }

    public class Included
    {
        public IncludedAttributes Attributes { get; set; }
    }

    public class IncludedAttributes
    {
        public string Name { get; set; }
    }

    public enum ShowTypeEnum
    {
        Movie,
        Ova,
        Ona,
        Tv,
        Music,
        Special
    }

    public enum Status
    {
        Current,
        Finished,
        Tba,
        Unreleased,
        Upcoming
    }
}