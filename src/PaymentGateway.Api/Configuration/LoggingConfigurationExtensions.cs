using Microsoft.Extensions.Logging.Console;

namespace PaymentGateway.Api.Configuration;

internal static class LoggingConfigurationExtensions
{
    internal static WebApplicationBuilder AddApplicationLogging(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.Configure(options =>
            options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);

        if (builder.Environment.IsDevelopment())
        {
            builder.Logging.AddSimpleConsole(ConfigureSimpleConsole);
        }
        else
        {
            builder.Logging.AddJsonConsole(ConfigureJsonConsole);
        }

        return builder;
    }

    private static void ConfigureSimpleConsole(SimpleConsoleFormatterOptions options)
    {
        options.IncludeScopes = true;
        options.SingleLine = true;
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
        options.UseUtcTimestamp = true;
    }

    private static void ConfigureJsonConsole(JsonConsoleFormatterOptions options)
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
        options.UseUtcTimestamp = true;
    }
}
