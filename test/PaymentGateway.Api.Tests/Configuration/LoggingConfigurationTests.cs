using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

using PaymentGateway.Api.Configuration;

namespace PaymentGateway.Api.Tests.Configuration;

public sealed class LoggingConfigurationTests
{
    [Fact]
    public void ConfiguresDevelopmentLogging()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        var result = builder.AddApplicationLogging();

        Assert.Same(builder, result);
    }
}
