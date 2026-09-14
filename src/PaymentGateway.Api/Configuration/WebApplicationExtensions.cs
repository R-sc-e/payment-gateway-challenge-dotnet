namespace PaymentGateway.Api.Configuration;

internal static class WebApplicationExtensions
{
    internal static WebApplication UsePaymentGatewayApi(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseSwagger();
        app.UseSwaggerUI();
        app.MapControllers();
        app.MapHealthChecks("/health/live");
        app.MapHealthChecks("/health/ready");

        return app;
    }
}
