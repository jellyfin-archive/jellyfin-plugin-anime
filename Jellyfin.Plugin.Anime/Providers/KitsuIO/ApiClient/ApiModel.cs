using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Anime.Providers.KitsuIO.ApiClient
{
    public partial class ApiSearchResponse
    {
        public List<Series> Data { get; set; }
        public ResponseMeta Meta { get; set; }
        public ResponseLinks Links { get; set; }
    }

    public partial class ApiGetResponse
    {
        public Series Data { get; set; }
        public List<Included> Included { get; set; }
    }

    public partial class Series
    {
        public long Id { get; set; }
        public DatumLinks Links { get; set; }
        public Attributes Attributes { get; set; }
        public Dictionary<string, Relationship> Relationships { get; set; }
    }

    public partial class Attributes
    {
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string Slug { get; set; }
        public string Synopsis { get; set; }
        public long CoverImageTopOffset { get; set; }
        public Titles Titles { get; set; }
        public string CanonicalTitle { get; set; }
        public List<string> AbbreviatedTitles { get; set; }
        public string AverageRating { get; set; }
        public Dictionary<string, long> RatingFrequencies { get; set; }
        public long? UserCount { get; set; }
        public long? FavoritesCount { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public DateTimeOffset? NextRelease { get; set; }
        public long? PopularityRank { get; set; }
        public long? RatingRank { get; set; }
        public string AgeRatingGuide { get; set; }
        public ShowTypeEnum Subtype { get; set; }
        public Status Status { get; set; }
        public string Tba { get; set; }
        public PosterImage PosterImage { get; set; }
        public CoverImage CoverImage { get; set; }
        public long? EpisodeCount { get; set; }
        public long? EpisodeLength { get; set; }
        public long? TotalLength { get; set; }
        public string YoutubeVideoId { get; set; }
        public ShowTypeEnum ShowType { get; set; }
        public bool Nsfw { get; set; }
        
        // Episode specific
        public int? Number { get; set; }
        public int? SeasonNumber { get; set; }
        public DateTime? AirDate { get; set; }
        public int? Length { get; set; }
    }

    public partial class CoverImage
    {
        public Uri Tiny { get; set; }
        public Uri Small { get; set; }
        public Uri Large { get; set; }
        public Uri Original { get; set; }
        public CoverImageMeta Meta { get; set; }
    }

    public partial class CoverImageMeta
    {
        public PurpleDimensions Dimensions { get; set; }
    }

    public partial class PurpleDimensions
    {
        public Large Tiny { get; set; }
        public Large Small { get; set; }
        public Large Large { get; set; }
    }

    public partial class Large
    {
        public long? Width { get; set; }
        public long? Height { get; set; }
    }

    public partial class PosterImage
    {
        public Uri Tiny { get; set; }
        public Uri Small { get; set; }
        public Uri Medium { get; set; }
        public Uri Large { get; set; }
        public Uri Original { get; set; }
        public PosterImageMeta Meta { get; set; }
    }

    public partial class PosterImageMeta
    {
        public FluffyDimensions Dimensions { get; set; }
    }

    public partial class FluffyDimensions
    {
        public Large Tiny { get; set; }
        public Large Small { get; set; }
        public Large Medium { get; set; }
        public Large Large { get; set; }
    }

    public partial class Titles
    {
        [JsonPropertyName("en")]
        public string En { get; set; }
        
        [JsonPropertyName("en_jp")]
        public string EnJp { get; set; }
        
        [JsonPropertyName("ja_jp")]
        public string JaJp { get; set; }
        
        [JsonPropertyName("en_us")]
        public string EnUs { get; set; }
        
        public string GetTitle =>
            !string.IsNullOrWhiteSpace(En) ? En :
            !string.IsNullOrWhiteSpace(EnUs) ? EnUs :
            !string.IsNullOrWhiteSpace(EnJp) ? EnJp :
            JaJp;

        public bool Equal(string title) =>
            En.Equals(title) ||
            EnUs.Equals(title) ||
            EnJp.Equals(title) ||
            JaJp.Equals(title);
    }

    public partial class DatumLinks
    {
        public Uri Self { get; set; }
    }

    public partial class Relationship
    {
        public RelationshipLinks Links { get; set; }
    }

    public partial class RelationshipLinks
    {
        public Uri Self { get; set; }
        public Uri Related { get; set; }
    }

    public partial class ResponseLinks
    {
        public Uri First { get; set; }
        public Uri Next { get; set; }
        public Uri Last { get; set; }
    }

    public partial class ResponseMeta
    {
        public long? Count { get; set; }
    }
    
    public partial class Included
    {
        public long? Id { get; set; }
        public string Type { get; set; }
        public IncludedAttributes Attributes { get; set; }
    }
    
    public partial class IncludedAttributes
    {
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public string Description { get; set; }
    }

    public enum ShowTypeEnum { Movie, Ova, Ona, Tv, Music, Special };

    public enum Status { Current, Finished, Tba, Unreleased, Upcoming };
}
