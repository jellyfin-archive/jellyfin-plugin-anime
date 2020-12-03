using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Anime.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Anime.Providers.AniSearch
{
    /// <summary>
    /// API for https://anisearch.com
    /// Anisearch does not have an API interface to work with
    /// </summary>
    internal class AniSearchApi
    {
        public static List<string> anime_search_names = new List<string>();
        public static List<string> anime_search_ids = new List<string>();
        public static string SearchLink = "https://www.anisearch.com/anime/index/?char=all&page=1&text={0}&smode=2&sort=title&order=asc&view=2&title=de,en,fr,it,pl,ru,es,tr&titlex=1,2&hentai=yes";
        public static string AniSearch_anime_link = "https://www.anisearch.com/anime/";

        static AniSearchApi()
        {
        }

        /// <summary>
        /// API call to get the anime with the id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static async Task<RemoteSearchResult> GetAnime(string id)
        {
            string WebContent = await WebRequestAPI(AniSearch_anime_link + id);
            var result = new RemoteSearchResult
            {
                Name = await SelectName(WebContent, Plugin.Instance.Configuration.TitlePreference, "en")
            };

            result.SearchProviderName = await One_line_regex(new Regex("\"" + "Japanisch" + "\"" + @"> <strong>(.*?)<\/"), WebContent);
            result.ImageUrl = await Get_ImageUrl(WebContent);
            result.SetProviderId(ProviderNames.AniSearch, id);
            result.Overview = await Get_Overview(WebContent);

            return result;
        }

        /// <summary>
        /// API call to select the language
        /// </summary>
        /// <param name="WebContent"></param>
        /// <param name="preference"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        private static async Task<string> SelectName(string WebContent, TitlePreferenceType preference, string language)
        {
            if (preference == TitlePreferenceType.Localized && language == "en")
                return await Get_title("en", WebContent);
            if (preference == TitlePreferenceType.Localized && language == "de")
                return await Get_title("de", WebContent);
            if (preference == TitlePreferenceType.Localized && language == "ger")
                return await Get_title("de", WebContent);
            if (preference == TitlePreferenceType.Japanese)
                return await Get_title("jap", WebContent);

            return await Get_title("jap_r", WebContent);
        }

        /// <summary>
        /// API call to get the title with the right language
        /// </summary>
        /// <param name="lang"></param>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public static async Task<string> Get_title(string lang, string WebContent)
        {
            switch (lang)
            {
                case "en":
                    return await One_line_regex(new Regex("\"" + "Englisch" + "\"" + @"> <strong>(.*?)<\/"), WebContent);

                case "de":
                    return await One_line_regex(new Regex("\"" + "Deutsch" + "\"" + @"> <strong>(.*?)<\/"), WebContent);

                case "jap":
                    return await One_line_regex(new Regex("<div class=\"grey\">" + @"(.*?)<\/"), await One_line_regex(new Regex("\"" + "Englisch" + "\"" + @"> <strong>(.*?)<\/div"), WebContent));

                //Default is jap_r
                default:
                    return await One_line_regex(new Regex("\"" + "Japanisch" + "\"" + @"> <strong>(.*?)<\/"), WebContent);
            }
        }

        /// <summary>
        /// API call to get the genre of the anime
        /// </summary>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public static async Task<List<string>> Get_Genre(string WebContent)
        {
            List<string> result = new List<string>();
            string Genres = await One_line_regex(new Regex("<ul class=\"cloud\">" + @"(.*?)<\/ul>"), WebContent);
            int x = 0;
            string AniSearch_Genre = null;
            while (AniSearch_Genre != "")
            {
                AniSearch_Genre = await One_line_regex(new Regex(@"<li>(.*?)<\/li>"), Genres, 0, x);
                AniSearch_Genre = await One_line_regex(new Regex("\">" + @"(.*?)<\/a>"), AniSearch_Genre);
                if (AniSearch_Genre != "")
                {
                    result.Add(AniSearch_Genre);
                }
                x++;
            }
            return result;
        }

        /// <summary>
        /// API call to get the image url
        /// </summary>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public static async Task<string> Get_ImageUrl(string WebContent)
        {
            return await One_line_regex(new Regex("<img itemprop=\"image\" src=\"" + @"(.*?)" + "\""), WebContent);
        }

        /// <summary>
        /// API call to get the rating
        /// </summary>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public static async Task<string> Get_Rating(string WebContent)
        {
            return await One_line_regex(new Regex("<span itemprop=\"ratingValue\">" + @"(.*?)" + @"<\/span>"), WebContent);
        }

        /// <summary>
        /// API call to get the description
        /// </summary>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public static async Task<string> Get_Overview(string WebContent)
        {
            return Regex.Replace(await One_line_regex(new Regex("<span itemprop=\"description\" lang=\"de\" id=\"desc-de\" class=\"desc-zz textblock\">" + @"(.*?)<\/span>"), WebContent), "<.*?>", String.Empty);
        }

        /// <summary>
        /// API call to search a title and return the right one back
        /// </summary>
        /// <param name="title"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<string> Search_GetSeries(string title, CancellationToken cancellationToken)
        {
            anime_search_names.Clear();
            anime_search_ids.Clear();
            string result = null;
            string result_text = null;
            string WebContent = await WebRequestAPI(string.Format(SearchLink, title));
            int x = 0;
            while (result_text != "")
            {
                result_text = await One_line_regex(new Regex("<th scope=\"row\" class=\"showpop\" data-width=\"200\"" + @".*?>(.*)<\/th>"), WebContent, 1, x);
                if (result_text != "")
                {
                    //get id
                    int _x = 0;
                    string a_name = null;
                    while (a_name != "")
                    {
                        try
                        {
                            string id = await One_line_regex(new Regex(@"anime\/(.*?),"), result_text);
                            a_name = Regex.Replace(await One_line_regex(new Regex(@"((<a|<d).*?>)(.*?)(<\/a>|<\/div>)"), result_text, 3, _x), "<.*?>", String.Empty);
                            if (a_name != "")
                            {
                                if (await Equals_check.Compare_strings(a_name, title, cancellationToken))
                                {
                                    return id;
                                }
                                if (Int32.TryParse(id, out int n))
                                {
                                    anime_search_names.Add(a_name);
                                    anime_search_ids.Add(id);
                                }
                            }
                        }
                        catch (Exception) { }

                        _x++;
                    }
                }
                x++;
            }
            return result;
        }

        /// <summary>
        /// API call to search a title and return a list back
        /// </summary>
        /// <param name="title"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<List<string>> Search_GetSeries_list(string title, CancellationToken cancellationToken)
        {
            List<string> result = new List<string>();
            string result_text = null;
            string WebContent = await WebRequestAPI(string.Format(SearchLink, title));
            int x = 0;
            while (result_text != "")
            {
                result_text = await One_line_regex(new Regex("<th scope=\"row\" class=\"showpop\" data-width=\"200\"" + @".*?>(.*)<\/th>"), WebContent, 1, x);
                if (result_text != "")
                {
                    //get id
                    int _x = 0;
                    string a_name = null;
                    while (a_name != "")
                    {
                        string id = await One_line_regex(new Regex(@"anime\/(.*?),"), result_text);
                        a_name = Regex.Replace(await One_line_regex(new Regex(@"((<a|<d).*?>)(.*?)(<\/a>|<\/div>)"), result_text, 3, _x), "<.*?>", String.Empty);
                        if (a_name != "")
                        {
                            if (await Equals_check.Compare_strings(a_name, title, cancellationToken))
                            {
                                result.Add(id);
                                return result;
                            }
                            if (Int32.TryParse(id, out int n))
                            {
                                result.Add(id);
                            }
                        }
                        _x++;
                    }
                }
                x++;
            }
            return result;
        }

        /// <summary>
        /// SEARCH Title
        /// </summary>
        public static async Task<string> FindSeries(string title, CancellationToken cancellationToken)
        {
            string aid = await Search_GetSeries(title, cancellationToken);
            if (!string.IsNullOrEmpty(aid))
            {
                return aid;
            }
            else
            {
                int x = 0;

                foreach (string a_name in anime_search_names)
                {
                    if (await Equals_check.Compare_strings(a_name, title, cancellationToken))
                    {
                        return anime_search_ids[x];
                    }

                    x++;
                }
            }

            aid = await Search_GetSeries(await Equals_check.Clear_name(title, cancellationToken), cancellationToken);
            if (!string.IsNullOrEmpty(aid))
            {
                return aid;
            }

            return null;
        }

        /// <summary>
        /// Simple regex
        /// </summary>
        public static async Task<string> One_line_regex(Regex regex, string match, int group = 1, int match_int = 0)
        {
            int x = 0;
            MatchCollection matches = await Task.Run(() => regex.Matches(match));
            foreach (Match _match in matches)
            {
                if (x == match_int)
                {
                    return await Task.Run(() => _match.Groups[group].Value);
                }
                x++;
            }
            return "";
        }

        /// <summary>
        /// GET website content from the link
        /// </summary>
        public static Task<string> WebRequestAPI(string link) {
            var httpClient = Plugin.Instance.GetHttpClient();
            return httpClient.GetStringAsync(link);
        }
    }
}
