using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using PaymentGateway.Api.Clients;

namespace PaymentGateway.Api.Tests.TestDoubles;

internal sealed class BankClientPaymentGatewayFactory : WebApplicationFactory<Program>
{
    public StubHttpMessageHandler BankTransport { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("AcquiringBank:BaseUrl", "http://bank.test/");
        builder.UseSetting("AcquiringBank:TimeoutSeconds", "10");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAcquiringBankClient>();
            services.AddHttpClient<IAcquiringBankClient, AcquiringBankClient>()
                .ConfigurePrimaryHttpMessageHandler(() => BankTransport);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new StubTimeProvider(
                new DateTimeOffset(2030, 6, 15, 12, 0, 0, TimeSpan.Zero)));
        });
    }
}
