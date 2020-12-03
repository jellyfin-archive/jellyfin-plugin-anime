using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Anime.Providers.KitsuIO.ApiClient
{
    internal class KitsuIoApi
    {
        private static readonly HttpClient _httpClient;
        private const string _apiBaseUrl = "https://kitsu.io/api/edge";
        private static readonly JsonSerializerOptions _serializerOptions;

        static KitsuIoApi()
        {
            _httpClient = Plugin.Instance.GetHttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));

            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            _serializerOptions.Converters.Add(new LongToStringConverter());
            _serializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }

        public static async Task<ApiListResponse> Search_Series(Dictionary<string, string> filters)
        {
            var filterString = string.Join("&",filters.Select(x => $"filter[{x.Key}]={x.Value}"));
            var pageString = "page[limit]=10";

            var responseStream = await _httpClient.GetStreamAsync($"{_apiBaseUrl}/anime?{filterString}&{pageString}");
            return await JsonSerializer.DeserializeAsync<ApiListResponse>(responseStream, _serializerOptions);
        }

        public static async Task<ApiResponse> Get_Series(string seriesId)
        {
            var responseStream = await _httpClient.GetStreamAsync($"{_apiBaseUrl}/anime/{seriesId}?include=genres");
            return await JsonSerializer.DeserializeAsync<ApiResponse>(responseStream, _serializerOptions);
        }

        public static async Task<ApiListResponse> Get_Episodes(string seriesId)
        {
            var result = new ApiListResponse();
            long episodeCount = 10;
            var step = 10;

            for (long offset = 0; offset < episodeCount; offset += step)
            {
                var queryString = $"?filter[mediaId]={seriesId}&page[limit]={step}&page[offset]={offset}";
                var responseStream = await _httpClient.GetStreamAsync($"{_apiBaseUrl}/episodes{queryString}");
                var response = await JsonSerializer.DeserializeAsync<ApiListResponse>(responseStream, _serializerOptions);

                episodeCount = response.Meta.Count.Value;
                result.Data.AddRange(response.Data);
            }

            return result;
        }

        public static async Task<ApiResponse> Get_Episode(string episodeId)
        {
            var filterString = $"/{episodeId}";
            var responseStream = await _httpClient.GetStreamAsync($"{_apiBaseUrl}/episodes{filterString}");
            return await JsonSerializer.DeserializeAsync<ApiResponse>(responseStream, _serializerOptions);
        }
    }
}
