using PaymentGateway.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddApplicationLogging();
builder.Services.AddApiServices();
builder.Services.AddPaymentGatewayServices(builder.Configuration);

var app = builder.Build();

app.UsePaymentGatewayApi();

app.Run();

public partial class Program;
