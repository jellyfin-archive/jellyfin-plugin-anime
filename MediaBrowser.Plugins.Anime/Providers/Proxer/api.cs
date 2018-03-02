using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using MediaBrowser.Plugins.Anime.Configuration;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Plugins.Anime.Providers.Proxer
{
    /// <summary>
    /// API for http://proxer.me/ german anime database.
    /// 🛈 Proxer does not have an API interface to work with
    /// </summary>
    internal class Api
    {
        public static List<string> anime_search_names = new List<string>();
        public static List<string> anime_search_ids = new List<string>();
        public static string SearchLink = "http://proxer.me/search?s=search&name={0}&typ=all-anime&tags=&notags=#top";
        public static string Proxer_anime_link = "http://proxer.me/info/";

        /// <summary>
        /// API call to get a anime with the id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static async Task<RemoteSearchResult> GetAnime(string id)
        {
            string WebContent = await WebRequestAPI(Proxer_anime_link + id);

            var result = new RemoteSearchResult
            {
                Name = await SelectName(WebContent, Plugin.Instance.Configuration.TitlePreference, "en")
            };

            result.SearchProviderName = await One_line_regex(new Regex(@">([\S\s]*?)<"), await One_line_regex(new Regex(@"<td><b>Original Titel<\/b><\/td>([\S\s]*?)\/td>"), WebContent));
            result.ImageUrl = await Get_ImageUrl(WebContent);
            result.SetProviderId(ProxerSeriesProvider.provider_name, id);
            result.Overview = await Get_Overview(WebContent);

            return result;
        }

        /// <summary>
        /// Get the right name lang
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
        /// API call to get the name in the called lang
        /// </summary>
        /// <param name="lang"></param>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public static async Task<string> Get_title(string lang, string WebContent)
        {
            switch (lang)
            {
                case "en":
                    return await One_line_regex(new Regex(@">([\S\s]*?)<"), await One_line_regex(new Regex(@"<td><b>Englischer Titel<\/b><\/td>([\S\s]*?)\/td>"), WebContent));

                case "de":

                    return await One_line_regex(new Regex(@">([\S\s]*?)<"), await One_line_regex(new Regex(@"<td><b>Deutscher Titel<\/b><\/td>([\S\s]*?)\/td>"), WebContent));

                case "jap":
                    return await One_line_regex(new Regex(@">([\S\s]*?)<"), await One_line_regex(new Regex(@"<td><b>Japanischer Titel<\/b><\/td>([\S\s]*?)\/td>"), WebContent));

                //Default is jap_r
                default:
                    return await One_line_regex(new Regex(@">([\S\s]*?)<"), await One_line_regex(new Regex(@"<td><b>Original Titel<\/b><\/td>([\S\s]*?)\/td>"), WebContent));
            }
        }

        /// <summary>
        /// API call to get the genres of the anime
        /// </summary>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public static async Task<List<string>> Get_Genre(string WebContent)
        {
            List<string> result = new List<string>();
            string Genres = await One_line_regex(new Regex(@"<b>Genre<\/b>((?:.*?\r?\n?)*)<\/tr>"), WebContent);
            int x = 1;
            string Proxer_Genre = null;
            while (Proxer_Genre != "")
            {
                Proxer_Genre = await One_line_regex(new Regex("\">" + @"((?:.*?\r?\n?)*)<"), Genres, 1, x);
                if (Proxer_Genre != "")
                {
                    result.Add(Proxer_Genre);
                }
                x++;
            }
            return result;
        }

        /// <summary>
        /// API call to get the ratings of the anime
        /// </summary>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public static async Task<string> Get_Rating(string WebContent)
        {
            return await One_line_regex(new Regex("<span class=\"average\">" + @"(.*?)<"), WebContent);
        }

        /// <summary>
        /// API call to get the ImageUrl if the anime
        /// </summary>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public static async Task<string> Get_ImageUrl(string WebContent)
        {
            return "http://" + await One_line_regex(new Regex("<img src=\"" + @"\/\/((?:.*?\r?\n?)*)" + "\""), WebContent);
        }

        /// <summary>
        /// API call to get the description of the anime
        /// </summary>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public static async Task<string> Get_Overview(string WebContent)
        {
            return await One_line_regex(new Regex(@"Beschreibung:<\/b><br>((?:.*?\r?\n?)*)<\/td>"), WebContent);
        }

        /// <summary>
        /// Search a title and return the right one back
        /// </summary>
        /// <param name="title"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<string> Search_GetSeries(string title, CancellationToken cancellationToken,bool bettersearchresults=false)
        {
            anime_search_names.Clear();
            anime_search_ids.Clear();
            string result = null;
            string result_text = null;
            string WebContent = "";
            if (bettersearchresults)
            {
                 WebContent = await WebRequestAPI(string.Format(SearchLink, Uri.EscapeUriString(await Equals_check.Half_string(title, cancellationToken, 4,60))));
            }
            else
            {
                 WebContent = await WebRequestAPI(string.Format(SearchLink, Uri.EscapeUriString(title)));
            }
            int x = 0;
            while (result_text != "")
            {
                result_text = await One_line_regex(new Regex("<tr align=\"" + @"left(.*?)tr>"), WebContent, 1, x);
                if (result_text != "")
                {
                    //get id
                    string id = await One_line_regex(new Regex("class=\"entry" + @"(.*?)" + "\">"), result_text);
                    string a_name = await One_line_regex(new Regex("#top\">" + @"(.*?)</a>"), result_text);
                    if (await Equals_check.Compare_strings(a_name, title, cancellationToken))
                    {
                        result = id;
                        return result;
                    }
                    if (Int32.TryParse(id, out int n))
                    {
                        anime_search_names.Add(a_name);
                        anime_search_ids.Add(id);
                    }
                }
                x++;
            }

            return result;
        }

        /// <summary>
        /// Search a title and return a list back
        /// </summary>
        /// <param name="title"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<List<string>> Search_GetSeries_list(string title, CancellationToken cancellationToken)
        {
            List<string> result = new List<string>();
            string result_text = null;
            string WebContent = await WebRequestAPI(string.Format(SearchLink, Uri.EscapeUriString(title)));
            int x = 0;
            while (result_text != "")
            {
                result_text = await One_line_regex(new Regex("<tr align=\"" + @"left(.*?)tr>"), WebContent, 1, x);
                if (result_text != "")
                {
                    //get id

                    string id = await One_line_regex(new Regex("class=\"entry" + @"(.*?)" + "\">"), result_text);
                    string a_name = await One_line_regex(new Regex("#top\">" + @"(.*?)</a>"), result_text);
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
                x++;
            }
            return result;
        }

        /// <summary>
        /// API call too find a series
        /// </summary>
        /// <param name="title"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
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
            aid = await Search_GetSeries(await Equals_check.Clear_name(title, cancellationToken), cancellationToken,true);
            if (!string.IsNullOrEmpty(aid))
            {
                return aid;
            }
            aid = await Search_GetSeries(await Equals_check.Clear_name_step2(title, cancellationToken), cancellationToken,true);
            if (!string.IsNullOrEmpty(aid))
            {
                return aid;
            }
            return null;
        }

        /// <summary>
        /// Simple async regex call
        /// </summary>
        /// <param name="regex"></param>
        /// <param name="match"></param>
        /// <param name="group"></param>
        /// <param name="match_int"></param>
        /// <returns></returns>
        public static async Task<string> One_line_regex(Regex regex, string match, int group = 1, int match_int = 0)
        {
            Regex _regex = regex;
            int x = 0;
            MatchCollection matches = await Task.Run(() => regex.Matches(match));
            foreach (Match _match in matches)
            {
                if (x == match_int)
                {
                    return await Task.Run(() => _match.Groups[group].Value.ToString());
                }
                x++;
            }
            return "";
        }

        /// <summary>
        /// Need too get the Webcontent for some API calls.
        /// </summary>
        /// <param name="link"></param>
        /// <returns></returns>
        public static async Task<string> WebRequestAPI(string link)
        {
            string _strContent = "";
            using (WebClient client = new WebClient())
            {
                client.Headers.Add(HttpRequestHeader.Cookie, "Adult=1");
                Task<string> async_content = client.DownloadStringTaskAsync(link);
                _strContent = await async_content;
            }
            return _strContent;
        }
    }
}