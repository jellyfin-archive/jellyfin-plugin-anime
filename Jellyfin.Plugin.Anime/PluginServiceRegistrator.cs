using System.Net.Http.Headers;
using System.Reflection;
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

        /// <inheritdoc />
        public void RegisterServices(IServiceCollection serviceCollection)
        {
            var provider = serviceCollection.BuildServiceProvider();
            var applicationHost = provider.GetRequiredService<IApplicationHost>();

            var productHeader = new ProductInfoHeaderValue(
                applicationHost.Name.Replace(' ', '-'),
                applicationHost.ApplicationVersionString);

            var pluginHeader = new ProductInfoHeaderValue(
                Constants.PluginName.Replace(' ', '-'),
                Assembly.GetExecutingAssembly().GetName().Version.ToString());

            serviceCollection.AddHttpClient(Constants.PluginGuid, c =>
                {
                    c.DefaultRequestHeaders.UserAgent.Add(productHeader);
                    c.DefaultRequestHeaders.UserAgent.Add(pluginHeader);
                    c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue($"({applicationHost.ApplicationUserAgentAddress})"));
                })
                .ConfigurePrimaryHttpMessageHandler(x => new DefaultHttpClientHandler());

        }
    }
}
