using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using PaymentGateway.Api;
using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Infrastructure;
using PaymentGateway.Api.Observability;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entry => string.IsNullOrWhiteSpace(entry.Key) ? "request" : entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The supplied value is invalid."
                        : error.ErrorMessage)
                    .ToArray());

        return new BadRequestObjectResult(ProblemResponses.Rejected(context.HttpContext, errors));
    };
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Payment Gateway API",
        Version = "v1",
        Description = "Processes card payments through the Checkout.com acquiring-bank simulator."
    });
});

builder.Services
    .AddOptions<AcquiringBankOptions>()
    .BindConfiguration(AcquiringBankOptions.SectionName)
    .ValidateDataAnnotations()
    .Validate(
        options => options.BaseUrl is { IsAbsoluteUri: true } &&
                   options.BaseUrl.Scheme is "http" or "https",
        "AcquiringBank:BaseUrl must be an absolute HTTP or HTTPS URL.")
    .ValidateOnStart();

builder.Services.AddHttpClient<IAcquiringBankClient, AcquiringBankClient>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<AcquiringBankOptions>>().Value;
    client.BaseAddress = options.BaseUrl;
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PaymentRequestValidator>();
builder.Services.AddSingleton<IPaymentRepository, InMemoryPaymentRepository>();
builder.Services.AddSingleton<PaymentTelemetry>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddHealthChecks();

var telemetryBuilder = builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(PaymentTelemetry.ServiceName))
    .WithTracing(tracing => tracing
        .AddSource(PaymentTelemetry.ActivitySourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics
        .AddMeter(PaymentTelemetry.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());

if (builder.Configuration.GetValue<bool>("Observability:ConsoleExporter"))
{
    telemetryBuilder.WithTracing(tracing => tracing.AddConsoleExporter());
    telemetryBuilder.WithMetrics(metrics => metrics.AddConsoleExporter());
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();

public partial class Program;