using FunctionAppDemo1;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

[assembly : FunctionsStartup(typeof(Startup))]

namespace FunctionAppDemo1
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddApplicationInsightsTelemetry();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            Microsoft.ApplicationInsights.AspNetCore.Extensions.ApplicationInsightsServiceOptions aiOptions
                = new Microsoft.ApplicationInsights.AspNetCore.Extensions.ApplicationInsightsServiceOptions();
            // Disables adaptive sampling.
            aiOptions.EnableAdaptiveSampling = true;

            // Disables QuickPulse (Live Metrics stream).
            aiOptions.EnableQuickPulseMetricStream = true;
            aiOptions.EnableDependencyTrackingTelemetryModule = true;
            aiOptions.EnableHeartbeat = true;
            aiOptions.EnableQuickPulseMetricStream = true;
            aiOptions.EnablePerformanceCounterCollectionModule = true;
            aiOptions.EnableAppServicesHeartbeatTelemetryModule = true;

            // The following line enables Application Insights telemetry collection.
            services.AddApplicationInsightsTelemetry(aiOptions);

            // This code adds other services for your application.
            services.AddMvcCore();
        }
    }
}
