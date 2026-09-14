using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Tests.TestDoubles;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Tests.Controllers;

public sealed class PaymentsControllerTests
{
    [Fact]
    public async Task LogsRejectedFieldNamesWithoutCardDetails()
    {
        const string cardNumber = "2222405343248877";
        const string cvv = "12";
        var paymentService = new StubPaymentService();
        var logger = new RecordingLogger<PaymentsController>();
        var controller = CreateController(paymentService, logger);

        var response = await controller.PostPayment(
            TestRequests.Valid(cardNumber: cardNumber, cvv: cvv),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Null(paymentService.LastRequest);
        var entry = Assert.Single(logger.Entries, item => item.Level == LogLevel.Information);
        Assert.Equal("cvv", entry.Properties["InvalidFields"]);
        Assert.DoesNotContain(cardNumber, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(cvv, entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PassesPostRequestToServiceAndMapsModelToResponse()
    {
        var result = new PaymentModel(
            Guid.NewGuid(), PaymentStatus.Authorized, "8877", 7, 2031, "GBP", 1050);
        var paymentService = new StubPaymentService { Result = result };
        var controller = CreateController(paymentService);
        var request = TestRequests.Valid(cardNumber: "2222405343248877", cvv: "123");

        var action = await controller.PostPayment(request, CancellationToken.None);

        Assert.Same(request, paymentService.LastRequest);
        var response = Assert.IsType<PaymentResponse>(Assert.IsType<OkObjectResult>(action.Result).Value);
        AssertResponseMatches(result, response);
    }

    [Theory]
    [InlineData(AcquiringBankFailure.Unavailable)]
    [InlineData(AcquiringBankFailure.Timeout)]
    [InlineData(AcquiringBankFailure.InvalidResponse)]
    public async Task MapsAcquiringBankFailureToBadGateway(AcquiringBankFailure failure)
    {
        var paymentService = new StubPaymentService
        {
            ExceptionToThrow = new AcquiringBankException(failure, "bank detail")
        };
        var controller = CreateController(paymentService);

        var action = await controller.PostPayment(TestRequests.Valid(), CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status502BadGateway, problem.Status);
        Assert.Equal("The acquiring bank is unavailable", problem.Title);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
        Assert.Null(problem.Detail);
    }

    [Fact]
    public void MapsQueryReadModelToResponse()
    {
        var result = new PaymentModel(
            Guid.NewGuid(), PaymentStatus.Declined, "1234", 8, 2032, "EUR", 250);
        var controller = CreateController(new StubPaymentService { Result = result });

        var action = controller.GetPayment(result.Id);

        var response = Assert.IsType<PaymentResponse>(Assert.IsType<OkObjectResult>(action.Result).Value);
        AssertResponseMatches(result, response);
    }

    private static PaymentsController CreateController(
        IPaymentService paymentService,
        ILogger<PaymentsController>? logger = null) =>
        new(
            new PaymentRequestValidator(new StubTimeProvider(
                new DateTimeOffset(2030, 6, 15, 12, 0, 0, TimeSpan.Zero))),
            paymentService,
            logger ?? new RecordingLogger<PaymentsController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

    private static void AssertResponseMatches(PaymentModel model, PaymentResponse response)
    {
        Assert.Equal(model.Id, response.Id);
        Assert.Equal(model.Status, response.Status);
        Assert.Equal(model.CardNumberLastFour, response.CardNumberLastFour);
        Assert.Equal(model.ExpiryMonth, response.ExpiryMonth);
        Assert.Equal(model.ExpiryYear, response.ExpiryYear);
        Assert.Equal(model.Currency, response.Currency);
        Assert.Equal(model.Amount, response.Amount);
    }

    private sealed class StubPaymentService : IPaymentService
    {
        public PaymentModel Result { get; init; } = new(
            Guid.NewGuid(), PaymentStatus.Authorized, "8877", 7, 2031, "GBP", 1050);

        public PostPaymentRequest? LastRequest { get; private set; }

        public Exception? ExceptionToThrow { get; init; }

        public PaymentModel? Get(Guid id) => Result;

        public Task<PaymentModel> ProcessAsync(
            PostPaymentRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Result);
        }
    }
}
