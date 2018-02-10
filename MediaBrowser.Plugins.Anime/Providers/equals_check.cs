using MediaBrowser.Model.Logging;
using MediaBrowser.Plugins.Anime.Providers.AniDB.Identity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MediaBrowser.Plugins.Anime.Providers
{
    internal class Equals_check
    {
        public readonly ILogger _logger;

        public Equals_check(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Clear name
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        public static string clear_name(string a)
        {
            try
            {
                a = a.Trim().Replace(one_line_regex(new Regex(@"(?s) \(.*?\)"), a.Trim(), 0), "");
            }
            catch (Exception)
            { }
            a = a.Replace(".", " ");
            a = a.Replace("-", " ");
            a = a.Replace("`", "");
            a = a.Replace("'", "");
            a = a.Replace("&", "and");
            a = a.Replace("(", "");
            a = a.Replace(")", "");
            try
            {
                a = a.Replace(one_line_regex(new Regex(@"(?s)(S[0-9]+)"), a.Trim()), one_line_regex(new Regex(@"(?s)S([0-9]+)"), a.Trim()));
            }
            catch (Exception)
            {
            }
            return a;
        }

        /// <summary>
        /// Clear name heavy.
        /// Example: Text & Text to Text and Text
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        public static string clear_name_step2(string a)
        {
            try
            {
                a = a.Trim().Replace(one_line_regex(new Regex(@"(?s) \(.*?\)"), a.Trim(), 0), "");
            }
            catch (Exception)
            { }
            a = a.Replace(".", " ");
            a = a.Replace("-", " ");
            a = a.Replace("`", "");
            a = a.Replace("'", "");
            a = a.Replace("&", "and");
            a = a.Replace(":", "");
            a = a.Replace("␣", "");
            a = a.Replace("2wei", "zwei");
            a = a.Replace("3rei", "drei");
            a = a.Replace("4ier", "vier");
            return a;
        }

        /// <summary>
        /// If a and b match it return true
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool Compare_strings(string a, string b)
        {
            if (!string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b))
            {
                if (simple_compare(a, b))
                    return true;
                if (Fast_xml_search(a, b))
                    return true;

                return false;
            }
            return false;
        }

        /// <summary>
        /// Cut p(%) away from the string
        /// </summary>
        /// <param name="string_"></param>
        /// <param name="min_lenght"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public static string Half_string(string string_, int min_lenght = 0, int p = 50)
        {
            decimal length = 0;
            if ((int)((decimal)string_.Length - (((decimal)string_.Length / 100m) * (decimal)p)) > min_lenght)
            {
                length = (decimal)string_.Length - (((decimal)string_.Length / 100m) * (decimal)p);
            }
            else
            {
                if (string_.Length < min_lenght)
                {
                    length = string_.Length;
                }
                else
                {
                    length = min_lenght;
                }
            }
            return string_.Substring(0, (int)length);
        }

        /// <summary>
        /// simple regex
        /// </summary>
        /// <param name="regex"></param>
        /// <param name="match"></param>
        /// <param name="group"></param>
        /// <param name="match_int"></param>
        /// <returns></returns>
        public static string one_line_regex(Regex regex, string match, int group = 1, int match_int = 0)
        {
            Regex _regex = regex;
            int x = 0;
            foreach (Match _match in regex.Matches(match))
            {
                if (x == match_int)
                {
                    return _match.Groups[group].Value.ToString();
                }
                x++;
            }
            return "";
        }

        /// <summary>
        ///Return true if a and b match return false if not
        ///It loads the titles.xml on exceptions
        /// </summary>
        private static bool Fast_xml_search(string a, string b, bool return_AniDBid = false, bool retry = false)
        {
            //Get AID aid=\"([s\S].*)\">
            try
            {
                List<string> pre_aid = new List<string>();
                string xml = File.ReadAllText(get_anidb_xml_file());
                int x = 0;
                string s1 = "-";
                string s2 = "-";
                while (!string.IsNullOrEmpty(s1) && !string.IsNullOrEmpty(s2))
                {
                    s1 = one_line_regex(new Regex("<anime aid=" + "\"" + @"(\d+)" + "\"" + @">(?>[^<>]+|<(?!\/anime>)[^<>]*>)*?" + Regex.Escape(Half_string(a, 4))), xml, 1, x);
                    if (s1 != "")
                    {
                        pre_aid.Add(s1);
                    }
                    s2 = one_line_regex(new Regex("<anime aid=" + "\"" + @"(\d+)" + "\"" + @">(?>[^<>]+|<(?!\/anime>)[^<>]*>)*?" + Regex.Escape(Half_string(b, 4))), xml, 1, x);
                    if (s1 != "")
                    {
                        if (s1 != s2)
                        {
                            pre_aid.Add(s2);
                        }
                    }
                    x++;
                }
                foreach (string _aid in pre_aid)
                {
                    XElement doc = XElement.Parse("<?xml version=\"1.0\" encoding=\"UTF - 8\"?>" + "<animetitles>" + one_line_regex(new Regex("<anime aid=\"" + _aid + "\">" + @"(?s)(.*?)<\/anime>"), xml, 0) + "</animetitles>");
                    var a_ = from page in doc.Elements("anime")
                             where _aid == page.Attribute("aid").Value
                             select page;
                    if (simple_compare(a_.Elements("title"), b) && simple_compare(a_.Elements("title"), a))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception)
            {
                if (retry)
                {
                    return false;
                }
                else
                {
                    Task.Run(() => AniDbTitleDownloader.Load_static(new System.Threading.CancellationToken()));
                    return Fast_xml_search(a, b, false, true);
                }
            }
        }

        /// <summary>
        /// Return the AniDB ID if a and b match
        /// </summary>
        public static string Fast_xml_search(string a, string b, bool return_AniDBid, int x_ = 0)
        {
            //Get AID aid=\"([s\S].*)\">
            try
            {
                List<string> pre_aid = new List<string>();
                string xml = File.ReadAllText(get_anidb_xml_file());
                int x = 0;
                string s1 = "-";
                string s2 = "-";
                while (!string.IsNullOrEmpty(s1) && !string.IsNullOrEmpty(s2))
                {
                    s1 = one_line_regex(new Regex("<anime aid=" + "\"" + @"(\d+)" + "\"" + @">(?>[^<>]+|<(?!\/anime>)[^<>]*>)*?" + Regex.Escape(Half_string(a, 4))), xml, 1, x);
                    if (s1 != "")
                    {
                        pre_aid.Add(s1);
                    }
                    s2 = one_line_regex(new Regex("<anime aid=" + "\"" + @"(\d+)" + "\"" + @">(?>[^<>]+|<(?!\/anime>)[^<>]*>)*?" + Regex.Escape(Half_string(b, 4))), xml, 1, x);
                    if (s1 != "")
                    {
                        if (s1 != s2)
                        {
                            pre_aid.Add(s2);
                        }
                    }
                    x++;
                }
                foreach (string _aid in pre_aid)
                {
                    XElement doc = XElement.Parse("<?xml version=\"1.0\" encoding=\"UTF - 8\"?>" + "<animetitles>" + one_line_regex(new Regex("<anime aid=\"" + _aid + "\">" + @"(?s)(.*?)<\/anime>"), xml, 0) + "</animetitles>");
                    var a_ = from page in doc.Elements("anime")
                             where _aid == page.Attribute("aid").Value
                             select page;
                    if (simple_compare(a_.Elements("title"), b) && simple_compare(a_.Elements("title"), a))
                    {
                        return _aid;
                    }
                }
                return "";
            }
            catch (Exception)
            {
                if (x_ == 1)
                {
                    return "";
                }
                else
                {
                    Task.Run(() => AniDbTitleDownloader.Load_static(new System.Threading.CancellationToken()));
                    return Fast_xml_search(a, b, true, 1);
                }
            }
        }

        /// <summary>
        /// get file Path from anidb xml file
        /// </summary>
        /// <returns></returns>
        private static string get_anidb_xml_file()
        {
            return AniDbTitleDownloader.TitlesFilePath_;
        }

        /// <summary>
        /// Compare 2 Strings, and it just works
        /// SeriesA S2 == SeriesA Second Season | True;
        /// </summary>
        private static bool simple_compare(string a, string b, bool fastmode = false)
        {
            if (fastmode)
            {
                if (a[0] == b[0])
                {
                }
                else
                {
                    return false;
                }
            }

            if (Core_compare(a, b))
                return true;
            if (Core_compare(b, a))
                return true;

            return false;
        }

        /// <summary>
        /// Compare 2 Strings, and it just works
        /// </summary>
        private static bool Core_compare(string a, string b)
        {
            if (a == b)
                return true;

            a = a.ToLower().Replace(" ", "").Trim().Replace(".", "");
            b = b.ToLower().Replace(" ", "").Trim().Replace(".", "");

            if (clear_name(a) == clear_name(b))
                return true;
            if (clear_name_step2(a) == clear_name_step2(b))
                return true;
            if (a.Replace("-", " ") == b.Replace("-", " "))
                return true;
            if (a.Replace(" 2", ":secondseason") == b.Replace(" 2", ":secondseason"))
                return true;
            if (a.Replace("2", "secondseason") == b.Replace("2", "secondseason"))
                return true;
            if (convert_symbols_too_numbers(a, "I") == convert_symbols_too_numbers(b, "I"))
                return true;
            if (convert_symbols_too_numbers(a, "!") == convert_symbols_too_numbers(b, "!"))
                return true;
            if (a.Replace("ndseason", "") == b.Replace("ndseason", ""))
                return true;
            if (a.Replace("ndseason", "") == b)
                return true;
            if (one_line_regex(new Regex(@"((.*)s([0 - 9]))"), a, 2) + one_line_regex(new Regex(@"((.*)s([0 - 9]))"), a, 3) == one_line_regex(new Regex(@"((.*)s([0 - 9]))"), b, 2) + one_line_regex(new Regex(@"((.*)s([0 - 9]))"), b, 3))
                if (!string.IsNullOrEmpty(one_line_regex(new Regex(@"((.*)s([0 - 9]))"), a, 2) + one_line_regex(new Regex(@"((.*)s([0 - 9]))"), a, 3)))
                    return true;
            if (one_line_regex(new Regex(@"((.*)s([0 - 9]))"), a, 2) + one_line_regex(new Regex(@"((.*)s([0 - 9]))"), a, 3) == b)
                if (!string.IsNullOrEmpty(one_line_regex(new Regex(@"((.*)s([0 - 9]))"), a, 2) + one_line_regex(new Regex(@"((.*)s([0 - 9]))"), a, 3)))
                    return true;
            if (a.Replace("rdseason", "") == b.Replace("rdseason", ""))
                return true;
            if (a.Replace("rdseason", "") == b)
                return true;
            try
            {
                if (a.Replace("2", "secondseason").Replace(one_line_regex(new Regex(@"(?s)\(.*?\)"), a, 0), "") == b.Replace("2", "secondseason").Replace(one_line_regex(new Regex(@"(?s)\(.*?\)"), b, 0), ""))
                    return true;
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace("2", "secondseason").Replace(one_line_regex(new Regex(@"(?s)\(.*?\)"), a, 0), "") == b)
                    return true;
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace(" 2", ":secondseason").Replace(one_line_regex(new Regex(@"(?s)\(.*?\)"), a, 0), "") == b.Replace(" 2", ":secondseason").Replace(one_line_regex(new Regex(@"(?s)\(.*?\)"), b, 0), ""))
                    return true;
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace(" 2", ":secondseason").Replace(one_line_regex(new Regex(@"(?s)\(.*?\)"), a, 0), "") == b)
                    return true;
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace(one_line_regex(new Regex(@"(?s)\(.*?\)"), a, 0), "") == b.Replace(one_line_regex(new Regex(@"(?s)\(.*?\)"), b, 0), ""))
                    return true;
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace(one_line_regex(new Regex(@"(?s)\(.*?\)"), a, 0), "") == b)
                    return true;
            }
            catch (Exception)
            {
            }
            try
            {
                if (b.Replace(one_line_regex(new Regex(@"(?s)\(.*?\)"), b, 0), "").Replace("  2", ": second Season") == a)
                    return true;
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace(" 2ndseason", ":secondseason") + " vs " + b == a)
                    return true;
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace(one_line_regex(new Regex(@"(?s)\(.*?\)"), a, 0), "").Replace("  2", ":secondseason") == b)
                    return true;
            }
            catch (Exception)
            {
            }
            return false;
        }

        /// <summary>
        /// Example: Convert II to 2
        /// </summary>
        /// <param name="input"></param>
        /// <param name="symbol"></param>
        /// <returns></returns>
        private static string convert_symbols_too_numbers(string input, string symbol)
        {
            try
            {
                string regex_c = "_";
                int x = 0;
                int highest_number = 0;
                while (!string.IsNullOrEmpty(regex_c))
                {
                    regex_c = one_line_regex(new Regex(@"(" + symbol + @"+)"), input.ToLower().Trim(), 1, x).Trim();
                    if (highest_number < regex_c.Count())
                        highest_number = regex_c.Count();
                    x++;
                }
                x = 0;
                string output = "";
                while (x != highest_number)
                {
                    output = output + symbol;
                    x++;
                }
                output = input.Replace(output, highest_number.ToString());
                if (string.IsNullOrEmpty(output))
                {
                    output = input;
                }
                return output;
            }
            catch (Exception)
            {
                return input;
            }
        }

        /// <summary>
        /// Simple Compare a XElemtent with a string
        /// </summary>
        /// <param name="a_"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private static bool simple_compare(IEnumerable<XElement> a_, string b)
        {
            bool ignore_date = true;
            string a_date = "";
            string b_date = "";

            string b_date_ = one_line_regex(new Regex(@"([0-9][0-9][0-9][0-9])"), b);
            if (!string.IsNullOrEmpty(b_date_))
            {
                b_date = b_date_;
            }
            if (!string.IsNullOrEmpty(b_date))
            {
                foreach (XElement a in a_)
                {
                    if (ignore_date)
                    {
                        string a_date_ = one_line_regex(new Regex(@"([0-9][0-9][0-9][0-9])"), a.Value);
                        if (!string.IsNullOrEmpty(a_date_))
                        {
                            a_date = a_date_;
                            ignore_date = false;
                        }
                    }
                }
            }
            if (!ignore_date)
            {
                if (a_date.Trim()==b_date.Trim())
                {
                    foreach (XElement a in a_)
                    {
                            if (simple_compare(a.Value, b, true))
                                return true;
                    }
                }
                else
                {
                    return false;
                }
                return false;
            }
            else
            {
                foreach (XElement a in a_)
                {
                    if (ignore_date)
                    {
                        if (simple_compare(a.Value, b, true))
                            return true;
                    }
                }
                return false;
            }
        }
    }
}