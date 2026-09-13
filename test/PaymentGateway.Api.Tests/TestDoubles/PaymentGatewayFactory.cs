using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Infrastructure;

namespace PaymentGateway.Api.Tests.TestDoubles;

internal sealed class PaymentGatewayFactory : WebApplicationFactory<Program>
{
    public StubBankClient Bank { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Observability:ConsoleExporter"] = "false"
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAcquiringBankClient>();
            services.AddSingleton<IAcquiringBankClient>(Bank);
            services.RemoveAll<IPaymentRepository>();
            services.AddSingleton<IPaymentRepository, InMemoryPaymentRepository>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new StubTimeProvider(
                new DateTimeOffset(2030, 6, 15, 12, 0, 0, TimeSpan.Zero)));
        });
    }
}