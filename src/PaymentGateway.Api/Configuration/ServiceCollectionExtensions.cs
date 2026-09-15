using System.Text.Json.Serialization;

using FluentValidation;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Infrastructure;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Validation;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace PaymentGateway.Api.Configuration;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services
            .AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = CreateInvalidModelStateResponse);
        
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = AddTraceIdentifier);
        
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(ConfigureSwagger);
        services.AddHealthChecks();

        return services;
    }

    internal static IServiceCollection AddPaymentGatewayServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AcquiringBankOptions>()
            .Bind(configuration.GetSection(AcquiringBankOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                HasValidBaseUrl,
                "AcquiringBank:BaseUrl must be an absolute HTTP or HTTPS URL.")
            .ValidateOnStart();

        services.AddHttpClient<IAcquiringBankClient, AcquiringBankClient>(ConfigureAcquiringBankClient);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IValidator<PostPaymentRequest>, PaymentRequestValidator>();
        services.AddSingleton<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentService, PaymentService>();

        return services;
    }

    private static BadRequestObjectResult CreateInvalidModelStateResponse(ActionContext context)
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

        var problem = new ValidationProblemDetails(errors)
        {
            Type = "https://httpstatuses.com/400",
            Title = "Payment request was rejected",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["paymentStatus"] = "Rejected";
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(problem);
    }

    private static void AddTraceIdentifier(ProblemDetailsContext context) =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

    private static void ConfigureSwagger(SwaggerGenOptions options)
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Payment Gateway API",
            Version = "v1",
            Description = "Processes card payments through the Checkout.com acquiring-bank simulator."
        });
    }

    private static bool HasValidBaseUrl(AcquiringBankOptions options) =>
        options.BaseUrl is { IsAbsoluteUri: true } &&
        options.BaseUrl.Scheme is "http" or "https";

    private static void ConfigureAcquiringBankClient(IServiceProvider services, HttpClient client)
    {
        var options = services.GetRequiredService<IOptions<AcquiringBankOptions>>().Value;
        client.BaseAddress = options.BaseUrl;
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }
}
