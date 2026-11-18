using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Services;

namespace SwashbucklerDiary.Rcl.Extensions
{
    public static partial class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRclDependencyInjection(this IServiceCollection services)
        {
            services.TryAddScoped<InputJSModule>();
            services.TryAddScoped<AudioInterop>();
            services.TryAddScoped<ScreenshotJSModule>();
            // Safe default for hosts that don't ship the MCP HTTP server.
            // The Maui host registers the real McpService BEFORE calling this.
            services.TryAddSingleton<IMcpService, NullMcpService>();
            // No-op keep-alive default; the Maui host registers the Android
            // foreground-service implementation BEFORE calling this method.
            services.TryAddSingleton<IMcpKeepAlive, NullMcpKeepAlive>();
            return services;
        }
    }
}
