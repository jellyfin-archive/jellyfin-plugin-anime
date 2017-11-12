using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Plugins.Anime.Providers.AniList
{
    public class AniListApiClient
    {
        public static readonly SemaphoreSlim ResourcePool = new SemaphoreSlim(1, 1);

        public class AccessToken
        {
            public string access_token { get; set; }
            public int expires_in { get; set; }
        }

        private const string ApiUrl = "https://anilist.co/api";
        private const string ClientId = "aphid-zmljg";
        private const string ClientSecret = "M37YedMnMm9DQ2D9pLoEeqM2Ul";

        private static readonly string RequestTokenUrl = $"{ApiUrl}/auth/access_token?grant_type=client_credentials&client_id={ClientId}&client_secret={ClientSecret}";
        private static readonly string AnimeUrlFormat = $"{ApiUrl}/anime/{{0}}";
        private static readonly string SearchUrlFormat = $"{ApiUrl}/anime/search/{{0}}";

        private readonly IHttpClient _http;
        private readonly ILogger _log;

        private static string _accessToken;
        private static DateTime _accessTokenExpires;
        private IJsonSerializer _jsonSerializer;

        public AniListApiClient(IHttpClient http, ILogManager logManager, IJsonSerializer jsonSerializer)
        {
            _http = http;
            _jsonSerializer = jsonSerializer;
            _log = logManager.GetLogger("AniList");
        }

        public Task<Anime> GetAnime(string id)
        {
            return Get<Anime>(string.Format(AnimeUrlFormat, id));
        }

        public Task<Anime[]> Search(string anime)
        {
            return Get<Anime[]>(string.Format(SearchUrlFormat, Uri.EscapeDataString(anime)));
        }

        private async Task<T> Get<T>(string url, int attemptsRemaining = 2)
        {
            if (DateTime.Now > _accessTokenExpires)
                await RefreshAccessToken().ConfigureAwait(false);

            try
            {
                var json = "";
                string urlWithToken = url;
                if (url.Contains("?"))
                    urlWithToken += $"&access_token={_accessToken}";
                else
                    urlWithToken += $"?access_token={_accessToken}";

                var _webRequest = WebRequest.Create(@"" + Uri.EscapeUriString(urlWithToken));
                using (var _response = _webRequest.GetResponse())
                using (var _content = _response.GetResponseStream())
                using (var _reader = new StreamReader(_content))
                {
                    json = _reader.ReadToEnd().Trim();
                }
                return _jsonSerializer.DeserializeFromString<T>(json);
            }
            catch
            {
                if (attemptsRemaining <= 1)
                    throw;

                attemptsRemaining--;
                await RefreshAccessToken().ConfigureAwait(false);
                return await Get<T>(url, attemptsRemaining).ConfigureAwait(false);
            }
        }

        private async Task RefreshAccessToken()
        {
            try
            {
                var options = new HttpRequestOptions
                {
                    Url = RequestTokenUrl
                };

                using (var response = await _http.Post(options).ConfigureAwait(false))
                {
                    var credentials = _jsonSerializer.DeserializeFromStream<AccessToken>(response.Content);
                    _accessToken = credentials.access_token;
                    _accessTokenExpires = DateTime.Now + TimeSpan.FromSeconds(credentials.expires_in);
                }
            }
            catch (Exception e)
            {
                _log.ErrorException("Failed to retrieve API access token", e);
            }
        }
    }
}