using System.Net.Http.Headers;
using MediaBrowser.Common;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Anime
{
    /// <summary>
    /// Register services.
    /// </summary>
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {

        private readonly IApplicationHost _appHost;

        public PluginServiceRegistrator(IApplicationHost appHost)
        {
            _appHost = appHost;
        }

        /// <inheritdoc />
        public void RegisterServices(IServiceCollection services)
        {
            var productHeader = new ProductInfoHeaderValue(
                _appHost.Name.Replace(' ', '-'),
                _appHost.ApplicationVersionString);

            var pluginHeader = new ProductInfoHeaderValue(
                Plugin.Instance.Name.Replace(' ', '-'),
                Plugin.Instance.Version.ToString());

            services.AddHttpClient(Plugin.Instance.Id.ToString(), c =>
                {
                    c.DefaultRequestHeaders.UserAgent.Add(productHeader);
                    c.DefaultRequestHeaders.UserAgent.Add(pluginHeader);
                    c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue($"({_appHost.ApplicationUserAgentAddress})"));
                })
                .ConfigurePrimaryHttpMessageHandler(x => new DefaultHttpClientHandler());

        }
    }
}
