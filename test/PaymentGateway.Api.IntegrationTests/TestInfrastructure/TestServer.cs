using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Infrastructure;

namespace PaymentGateway.Api.Tests.TestInfrastructure;

public sealed class TestServer : IAsyncLifetime
{
    private readonly IReadOnlyDictionary<string, string?> _settings;
    private WebApplicationFactory<Program>? _application;
    private HttpClient? _client;
    private HttpMockServer? _acquiringBankServer;

    public TestServer()
        : this(new Dictionary<string, string?>())
    {
    }

    internal TestServer(IReadOnlyDictionary<string, string?> settings)
    {
        _settings = settings;
    }

    public HttpClient Client => _client ??
        throw new InvalidOperationException("The test client has not been initialized.");

    public PaymentRepository PaymentsRepository =>
        (PaymentRepository)Application.Services.GetRequiredService<IPaymentRepository>();

    public HttpMockServer AcquiringBankServer => _acquiringBankServer ??
        throw new InvalidOperationException("The test acquiring bank server has not been initialized.");

    private WebApplicationFactory<Program> Application => _application ??
        throw new InvalidOperationException("The test server has not been initialized.");

    public Task InitializeAsync()
    {
        _acquiringBankServer = new HttpMockServer();
        _application = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("AcquiringBank:BaseUrl", AcquiringBankServer.Url);
                builder.UseSetting("AcquiringBank:TimeoutSeconds", "10");

                foreach (var setting in _settings)
                {
                    builder.UseSetting(setting.Key, setting.Value);
                }
            });

        _client = _application.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();

        if (_application is not null)
        {
            await _application.DisposeAsync();
        }

        _acquiringBankServer?.Dispose();
    }
}
