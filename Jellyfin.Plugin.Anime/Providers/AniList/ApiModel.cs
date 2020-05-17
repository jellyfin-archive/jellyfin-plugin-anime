using System;
using System.Collections.Generic;
using System.Text;
using MediaBrowser.Model.Providers;
using Jellyfin.Plugin.Anime.Configuration;

namespace Jellyfin.Plugin.Anime.Providers.AniList
{
    using System.Collections.Generic;
    
    public class Title
    {
        public string romaji { get; set; }
        public string english { get; set; }
        public string native { get; set; }
    }

    public class CoverImage
    {
        public string medium { get; set; }
        public string large { get; set; }
    }

    public class ApiDate
    {
        public int? year { get; set; }
        public int? month { get; set; }
        public int? day { get; set; }
    }

    public class Page
    {
        public List<Media> media { get; set; }
    }

    public class Data
    {
        public Page Page { get; set; }
        public Media Media { get; set; }
    }

    public class Media
    {
        public int? averageScore { get; set; }
        public object bannerImage { get; set; }
        public object chapters { get; set; }
        public Characters characters { get; set; }
        public CoverImage coverImage { get; set; }
        public string description { get; set; }
        public int? duration { get; set; }
        public ApiDate endDate { get; set; }
        public int? episodes { get; set; }
        public string format { get; set; }
        public List<string> genres { get; set; }
        public object hashtag { get; set; }
        public int id { get; set; }
        public bool isAdult { get; set; }
        public int? meanScore { get; set; }
        public object nextAiringEpisode { get; set; }
        public int? popularity { get; set; }
        public string season { get; set; }
        public int? seasonYear { get; set; }
        public ApiDate startDate { get; set; }
        public string status { get; set; }
        public List<object> synonyms { get; set; }
        public List<Tag> tags { get; set; }
        public Title title { get; set; }
        public string type { get; set; }
        public object volumes { get; set; }

        /// <summary>
        /// API call to get the title in configured language
        /// </summary>
        /// <param name="preference"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public string GetPreferredTitle(TitlePreferenceType preference, string language)
        {
            if (preference == TitlePreferenceType.Localized)
            {
                if (language == "en")
                    return this.title.english;
                if (language == "jap")
                    return this.title.native;
            }
            if (preference == TitlePreferenceType.Japanese)
                return this.title.native;

            return this.title.romaji;
        }

        /// <summary>
        /// API call to get the img url
        /// </summary>
        /// <returns></returns>
        public string GetImageUrl()
        {
            return this.coverImage.large ?? this.coverImage.medium;
        }

        /// <summary>
        /// API call too get the rating, normalized to 1-10
        /// </summary>
        /// <returns></returns>
        public float GetRating()
        {
            return (this.averageScore ?? 0) / 10f;
        }

        /// <summary>
        /// Returns the start date as a DateTime object or null if not available
        /// </summary>
        /// <returns></returns>
        public DateTime? GetStartDate()
        {
            if (this.startDate.year == null || this.startDate.month == null || this.startDate.day == null)
                return null;
            return new DateTime(this.startDate.year.Value, this.startDate.month.Value, this.startDate.day.Value);
        }

        /// <summary>
        /// Returns the end date as a DateTime object or null if not available
        /// </summary>
        /// <returns></returns>
        public DateTime? GetEndDate()
        {
            if (this.endDate.year == null || this.endDate.month == null || this.endDate.day == null)
                return null;
            return new DateTime(this.endDate.year.Value, this.endDate.month.Value, this.endDate.day.Value);
        }

        /// <summary>
        /// Convert a Media object to a RemoteSearchResult
        /// </summary>
        /// <returns></returns>
        public RemoteSearchResult ToSearchResult()
        {
            var result = new RemoteSearchResult
            {
                Name = this.title.romaji,  // TODO: Call GetPreferredTitle() here
                Overview = this.description,
                ProductionYear = this.startDate.year,
                PremiereDate = this.GetStartDate(),
                ImageUrl = this.GetImageUrl(),
                SearchProviderName = ProviderNames.AniList,
                ProviderIds = new Dictionary<string, string>() {{ProviderNames.AniList, this.id.ToString()}}
            };
            return result;
        }
    }
    public class PageInfo
    {
        public int total { get; set; }
        public int perPage { get; set; }
        public bool hasNextPage { get; set; }
        public int currentPage { get; set; }
        public int lastPage { get; set; }
    }

    public class Name
    {
        public string first { get; set; }
        public string last { get; set; }
    }

    public class Image
    {
        public string medium { get; set; }
        public string large { get; set; }
    }

    public class Node
    {
        public int id { get; set; }
        public Name name { get; set; }
        public Image image { get; set; }
    }

    public class Name2
    {
        public string first { get; set; }
        public string last { get; set; }
        public string native { get; set; }
    }

    public class Image2
    {
        public string medium { get; set; }
        public string large { get; set; }
    }

    public class VoiceActor
    {
        public int id { get; set; }
        public Name2 name { get; set; }
        public Image2 image { get; set; }
        public string language { get; set; }
    }

    public class Edge
    {
        public Node node { get; set; }
        public string role { get; set; }
        public List<VoiceActor> voiceActors { get; set; }
    }

    public class Characters
    {
        public PageInfo pageInfo { get; set; }
        public List<Edge> edges { get; set; }
    }

    public class Tag
    {
        public int id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string category { get; set; }
    }

    public class RootObject
    {
        public Data data { get; set; }
    }
}