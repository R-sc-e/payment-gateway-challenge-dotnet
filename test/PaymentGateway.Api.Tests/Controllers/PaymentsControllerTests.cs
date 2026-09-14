using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Controllers;

public sealed class PaymentsControllerTests
{
    private readonly Mock<IValidator<PostPaymentRequest>> _validatorMock;
    private readonly Mock<IPaymentService> _paymentServiceMock;
    private readonly PaymentsController _controller;
    private readonly PostPaymentRequest _validRequest;

    public PaymentsControllerTests()
    {
        _validatorMock = new Mock<IValidator<PostPaymentRequest>>();

        _paymentServiceMock = new Mock<IPaymentService>();

        _validRequest = new PostPaymentRequest(
            "2222405343248877", 7, 2031, "GBP", 1050, "123");

        _validatorMock
            .Setup(validator => validator.ValidateAsync(
                It.IsAny<PostPaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _controller = new PaymentsController(
            _validatorMock.Object,
            _paymentServiceMock.Object,
            NullLogger<PaymentsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task PostPayment_PassesRequestToServiceAndMapsModelToResponse()
    {
        // Arrange
        var result = new PaymentModel(
            Guid.NewGuid(), PaymentStatus.Authorized, "8877", 7, 2031, "GBP", 1050);
        _paymentServiceMock
            .Setup(service => service.ProcessAsync(_validRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var action = await _controller.PostPayment(_validRequest, CancellationToken.None);

        // Assert
        _validatorMock.Verify(
            validator => validator.ValidateAsync(_validRequest, CancellationToken.None),
            Times.Once());
        _paymentServiceMock.Verify(
            service => service.ProcessAsync(_validRequest, CancellationToken.None),
            Times.Once());
        var response = Assert.IsType<PaymentResponse>(
            Assert.IsType<OkObjectResult>(action.Result).Value);
        AssertResponseMatches(result, response);
    }

    [Theory]
    [InlineData(AcquiringBankFailure.Unavailable)]
    [InlineData(AcquiringBankFailure.Timeout)]
    [InlineData(AcquiringBankFailure.InvalidResponse)]
    public async Task PostPayment_MapsAcquiringBankFailureToBadGateway(
        AcquiringBankFailure failure)
    {
        // Arrange
        _paymentServiceMock
            .Setup(service => service.ProcessAsync(
                It.IsAny<PostPaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AcquiringBankException(failure, "bank detail"));

        // Act
        var action = await _controller.PostPayment(_validRequest, CancellationToken.None);

        // Assert
        var result = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);

        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status502BadGateway, problem.Status);
        Assert.Equal("The acquiring bank is unavailable", problem.Title);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
        Assert.Null(problem.Detail);
    }

    [Fact]
    public void GetPayment_MapsQueryReadModelToResponse()
    {
        // Arrange
        var result = new PaymentModel(
            Guid.NewGuid(), PaymentStatus.Declined, "1234", 8, 2032, "EUR", 250);
        _paymentServiceMock
            .Setup(service => service.Get(result.Id))
            .Returns(result);

        // Act
        var action = _controller.GetPayment(result.Id);

        // Assert
        _paymentServiceMock.Verify(service => service.Get(result.Id), Times.Once());
        var response = Assert.IsType<PaymentResponse>(
            Assert.IsType<OkObjectResult>(action.Result).Value);
        AssertResponseMatches(result, response);
    }

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
}
