using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Plugins.Anime.Configuration;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Plugins.Anime.Providers.MyAnimeList
{
    /// <summary>
    /// This API use the WebContent of MyAnimelist and the API of MyAnimelist
    /// </summary>
    public class Api
    {
        public List<string> anime_search_names = new List<string>();
        public List<string> anime_search_ids = new List<string>();
        private static ILogManager _log;
        //Use API too search
        public string SearchLink = "https://myanimelist.net/api/anime/search.xml?q={0}";
        //Web Fallback search
        public string FallbackSearchLink= "https://myanimelist.net/search/all?q={0}";
        //No API funktion exist too get anime
        public string anime_link = "https://myanimelist.net/anime/";


        /// <summary>
        /// WebContent API call to get a anime with id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Api(ILogManager logManager)
        {
            _log = logManager;
        }
        public async Task<RemoteSearchResult> GetAnime(string id, CancellationToken cancellationToken)
        {
            string WebContent = await WebRequestAPI(anime_link + id, cancellationToken);

            var result = new RemoteSearchResult
            {
                Name = await SelectName(WebContent, Plugin.Instance.Configuration.TitlePreference, "en", cancellationToken)
            };

            result.SearchProviderName = WebUtility.HtmlDecode(await One_line_regex(new Regex("<span itemprop=\"name\">" + @"(.*?)<"), WebContent));
            result.ImageUrl = await Get_ImageUrlAsync(WebContent);
            result.SetProviderId(MyAnimeListSeriesProvider.provider_name, id);
            result.Overview = await Get_OverviewAsync(WebContent);

            return result;
        }

        /// <summary>
        ///  WebContent API call to select a prefence title
        /// </summary>
        /// <param name="WebContent"></param>
        /// <param name="preference"></param>
        /// <param name="language"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<string> SelectName(string WebContent, TitlePreferenceType preference, string language, CancellationToken cancellationToken)
        {
            if (preference == TitlePreferenceType.Localized && language == "en")
                return await Get_title("en", WebContent);
            if (preference == TitlePreferenceType.Japanese)
                return await Get_title("jap", WebContent);

            return await Get_title("jap_r", WebContent);
        }

        /// <summary>
        /// WebContent API call get a specific title
        /// </summary>
        /// <param name="lang"></param>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public async Task<string> Get_title(string lang, string WebContent)
        {
            switch (lang)
            {
                case "en":
                    return WebUtility.HtmlDecode(await One_line_regex(new Regex(@">([\S\s]*?)<"), await One_line_regex(new Regex(@"English:<\/span>(?s)(.*?)<"), WebContent)));

                case "jap":
                    return WebUtility.HtmlDecode(await One_line_regex(new Regex(@">([\S\s]*?)<"), await One_line_regex(new Regex(@"Japanese:<\/span>(?s)(.*?)<"), WebContent)));

                //Default is jap_r
                default:
                    return WebUtility.HtmlDecode(await One_line_regex(new Regex("<span itemprop=\"name\">" + @"(.*?)<"), WebContent));
            }
        }

        /// <summary>
        /// WebContent API call get genre
        /// </summary>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public async Task<List<string>> Get_GenreAsync(string WebContent)
        {
            List<string> result = new List<string>();
            try
            {
                string Genres = await One_line_regex(new Regex(@"\.setTargeting\(" + "\"genres\"" + @", \[(.*?)\])"), WebContent);
                int x = 1;
                Genres = Genres.Replace("\"", "");
                foreach (string Genre in Genres.Split(','))
                {
                    if (!string.IsNullOrEmpty(Genre))
                    {
                        result.Add(Genre);
                    }
                    x++;
                }
                return result;
            }
            catch (Exception)
            {
                result.Add("");
                return result;
            }
        }

        /// <summary>
        /// WebContent API call get rating
        /// </summary>
        /// <param name="WebContent"></param>
        public async Task<string> Get_RatingAsync(string WebContent)
        {
            return await One_line_regex(new Regex("<span itemprop=\"ratingValue\">" + @"(.*?)<"), WebContent);
        }

        /// <summary>
        /// WebContent API call to get the imgurl
        /// </summary>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public async Task<string> Get_ImageUrlAsync(string WebContent)
        {
            return await One_line_regex(new Regex("src=\"" + @"(?s)(.*?)" + "\""), await One_line_regex(new Regex(" < div style=\"text - align: center; \">" + @"(?s)(.*?)alt="), WebContent));
        }

        /// <summary>
        /// WebContent API call to get the description
        /// </summary>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public async Task<string> Get_OverviewAsync(string WebContent)
        {
            return System.Net.WebUtility.HtmlDecode(await One_line_regex(new Regex("itemprop=\\"+'"'+"description\\"+'"'+@">(.*?)<\/span>"), WebContent));
        }

        /// <summary>
        /// MyAnimeListAPI call to search the right series
        /// </summary>
        /// <param name="title"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<string> Search_GetSeries(string title, CancellationToken cancellationToken)
        {
            anime_search_names.Clear();
            anime_search_ids.Clear();
            string result_text = null;
            //API
            if (!string.IsNullOrEmpty(Plugin.Instance.Configuration.MyAnimeList_API_Name) && !string.IsNullOrEmpty(Plugin.Instance.Configuration.MyAnimeList_API_Pw))
            {
                string WebContent = await WebRequestAPI(string.Format(SearchLink, Uri.EscapeUriString(title)), cancellationToken, Plugin.Instance.Configuration.MyAnimeList_API_Name, Plugin.Instance.Configuration.MyAnimeList_API_Pw);
            int x = 0;
            while (result_text != "")
            {
                result_text = await One_line_regex(new Regex(@"<entry>(.*?)<\/entry>"), WebContent, 1, x);
                if (result_text != "")
                {
                    //get id
                    string id = await One_line_regex(new Regex(@"<id>(.*?)<\/id>"), result_text);
                    string a_name = await One_line_regex(new Regex(@"<title>(.*?)<\/title>"), result_text);
                    string b_name = await One_line_regex(new Regex(@"<english>(.*?)<\/english>"), result_text);
                    string c_name = await One_line_regex(new Regex(@"<synonyms>(.*?)<\/synonyms>"), result_text);

                    if (await Equals_check.Compare_strings(a_name, title, cancellationToken))
                    {
                        return id;
                    }
                    if (await Equals_check.Compare_strings(b_name, title, cancellationToken))
                    {
                        return id;
                    }
                    foreach (string d_name in c_name.Split(';'))
                    {
                        if (await Equals_check.Compare_strings(d_name, title, cancellationToken))
                        {
                            return id;
                        }
                    }

                    if (Int32.TryParse(id, out int n))
                    {
                        anime_search_names.Add(a_name);
                        anime_search_ids.Add(id);
                    }
                }
                x++;
            }
            }
            else
            {
                //Fallback to Web
                string WebContent = await WebRequestAPI(string.Format(FallbackSearchLink, Uri.EscapeUriString(title)), cancellationToken);
                string Regex_id = "-";
                int x = 0;
                while (!string.IsNullOrEmpty(Regex_id))
                {
                    Regex_id = "";
                    Regex_id = await One_line_regex(new Regex(@"(#revInfo(.*?)" + '"' + "(>(.*?)<))"), WebContent, 2, x);
                    String Regex_name = await One_line_regex(new Regex(@"(#revInfo(.*?)" + '"' + "(>(.*?)<))"), WebContent, 4, x);
                    if (!string.IsNullOrEmpty(Regex_id) && !string.IsNullOrEmpty(Regex_name))
                    {
                        try
                        {
                            int.Parse(Regex_id);

                            if (await Equals_check.Compare_strings(Regex_name, title, cancellationToken))
                            {
                                return Regex_id;
                            }
                        }
                        catch (Exception)
                        {
                            //AnyLog
                        }
                    }
                    else
                    {
                        Regex_id = "";
                    }
                    x++;
                }

            }
            return "";
        }

        /// <summary>
        /// MyAnimeListAPI call to search the series and return a list
        /// </summary>
        /// <param name="title"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<List<string>> Search_GetSeries_list(string title, CancellationToken cancellationToken)
        {
            List<string> result = new List<string>();
            string result_text = null;
            //API
            if (!string.IsNullOrEmpty(Plugin.Instance.Configuration.MyAnimeList_API_Name) && !string.IsNullOrEmpty(Plugin.Instance.Configuration.MyAnimeList_API_Pw))
            {
                string WebContent = await WebRequestAPI(string.Format(SearchLink, Uri.EscapeUriString(title)), cancellationToken, Plugin.Instance.Configuration.MyAnimeList_API_Name, Plugin.Instance.Configuration.MyAnimeList_API_Pw);
                int x = 0;
                while (result_text != "")
                {
                    result_text = await One_line_regex(new Regex(@"<entry>(.*?)<\/entry>"), WebContent, 1, x);
                    if (result_text != "")
                    {
                        //get id
                        string id = await One_line_regex(new Regex(@"<id>(.*?)<\/id>"), result_text);
                        string a_name = await One_line_regex(new Regex(@"<title>(.*?)<\/title>"), result_text);
                        string b_name = await One_line_regex(new Regex(@"<english>(.*?)<\/english>"), result_text);
                        string c_name = await One_line_regex(new Regex(@"<synonyms>(.*?)<\/synonyms>"), result_text);

                        if (await Equals_check.Compare_strings(a_name, title, cancellationToken))
                        {
                            result.Add(id);
                            return result;
                        }
                        if (await Equals_check.Compare_strings(b_name, title, cancellationToken))
                        {
                            result.Add(id);
                            return result;
                        }
                        foreach (string d_name in c_name.Split(';'))
                        {
                            if (await Equals_check.Compare_strings(d_name, title, cancellationToken))
                            {
                                result.Add(id);
                                return result;
                            }
                        }
                        if (Int32.TryParse(id, out int n))
                        {
                            result.Add(id);
                        }
                    }
                    x++;
                }
            }
            else
            {
                //Fallback to Web
                string WebContent = await WebRequestAPI(string.Format(FallbackSearchLink, Uri.EscapeUriString(title)), cancellationToken);
                string regex_id = "-";
                int x = 0;
                while (!string.IsNullOrEmpty(regex_id))
                {
                    regex_id = "";
                    regex_id=await One_line_regex(new Regex(@"(#revInfo(.*?)"+'"'+"(>(.*?)<))"), WebContent, 2, x);
                    if (!string.IsNullOrEmpty(regex_id))
                    {
                        try
                        {
                            int.Parse(regex_id);

                            if (await Equals_check.Compare_strings(await One_line_regex(new Regex(@"(#revInfo(.*?)" + '"' + "(>(.*?)<))"), WebContent, 4, x), title, cancellationToken))
                            {
                                result.Add(regex_id);
                                return result;
                            }
                        }
                        catch (Exception)
                        {
                            //AnyLog
                        }
                    }
                    x++;
                }

            }
            return result;
        }

        /// <summary>
        /// MyAnimeListAPI call to find a series
        /// </summary>
        /// <param name="title"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<string> FindSeries(string title, CancellationToken cancellationToken)
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
        /// simple regex
        /// </summary>
        /// <param name="regex"></param>
        /// <param name="match"></param>
        /// <param name="group"></param>
        /// <param name="match_int"></param>
        /// <returns></returns>
        public async Task<string> One_line_regex(Regex regex, string match, int group = 1, int match_int = 0)
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
        /// A WebRequestAPI too handle the Webcontent and the API of MyAnimeList
        /// </summary>
        /// <param name="link"></param>
        /// <param name="name"></param>
        /// <param name="pw"></param>
        /// <returns></returns>
        public async Task<string> WebRequestAPI(string link, CancellationToken cancellationToken, string name = null, string pw = null)
        {
            try
            {
                string encoded = await Task.Run(() => Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(name + ":" + pw)), cancellationToken);
                string _strContent;
                using (WebClient client = new WebClient())
                {
                    if (!await Task.Run(() => string.IsNullOrEmpty(name), cancellationToken) && !await Task.Run(() => string.IsNullOrEmpty(pw), cancellationToken))
                    {
                        client.Headers.Add("Authorization", "Basic " + encoded);
                    }
                    Task<string> async_content = client.DownloadStringTaskAsync(link);
                    _strContent = await async_content;
                }
                return _strContent;
            }
            catch (WebException)
            {
                return "";
            }
        }
    }
}