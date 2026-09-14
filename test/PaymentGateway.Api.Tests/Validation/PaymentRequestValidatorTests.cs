using FluentValidation.TestHelper;

using Moq;

using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Tests.Validation;

public sealed class PaymentRequestValidatorTests
{
    private static readonly DateTimeOffset Now =
        new(2030, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly PaymentRequestValidator _validator;

    public PaymentRequestValidatorTests()
    {
        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock
            .Setup(provider => provider.GetUtcNow())
            .Returns(Now);
        _validator = new PaymentRequestValidator(_timeProviderMock.Object);
    }

    [Theory]
    [InlineData("12345678901234")]
    [InlineData("1234567890123456789")]
    [InlineData("00000000000000")]
    public void Validate_AcceptsCardNumberBoundaryValues(string cardNumber)
    {
        // Arrange
        var request = new PostPaymentRequest(cardNumber, 7, 2031, "GBP", 1050, "123");

        // Act
        var errors = _validator.TestValidate(request);

        // Assert
        Assert.DoesNotContain(errors.Errors, failure => failure.PropertyName == "cardNumber");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("             ")]
    [InlineData("1234567890123")]
    [InlineData("12345678901234567890")]
    [InlineData("1234567890123a")]
    [InlineData("1234567890123-")]
    [InlineData("12345678901234\n")]
    public void Validate_RejectsInvalidCardNumbers(string? cardNumber)
    {
        // Arrange
        var request = new PostPaymentRequest(cardNumber, 7, 2031, "GBP", 1050, "123");

        // Act
        var errors = _validator.TestValidate(request);

        // Assert
        Assert.Contains(errors.Errors, failure => failure.PropertyName == "cardNumber");
    }

    [Fact]
    public void Validate_ReturnsStablePropertyNameAndMessage()
    {
        // Arrange
        var request = new PostPaymentRequest("123", 7, 2031, "GBP", 1050, "123");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        var failure = Assert.Single(result.Errors, error => error.PropertyName == "cardNumber");
        Assert.Equal("Card number must be between 14 and 19 characters.", failure.ErrorMessage);
    }

    [Fact]
    public void Validate_StopsEvaluatingPropertyAfterFirstFailure()
    {
        // Arrange
        var request = new PostPaymentRequest("abc", 7, 2031, "GBP", 1050, "123");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        var failure = Assert.Single(result.Errors, error => error.PropertyName == "cardNumber");
        Assert.Equal("Card number must be between 14 and 19 characters.", failure.ErrorMessage);
    }

    [Theory]
    [InlineData(6, 2030)]
    [InlineData(7, 2030)]
    [InlineData(1, 2031)]
    public void Validate_AcceptsCurrentAndFutureExpiryMonths(int month, int year)
    {
        // Arrange
        var request = new PostPaymentRequest(
            "2222405343248877", month, year, "GBP", 1050, "123");

        // Act
        var errors = _validator.TestValidate(request);

        // Assert
        Assert.DoesNotContain(errors.Errors, failure => failure.PropertyName == "expiryMonth");
        Assert.DoesNotContain(errors.Errors, failure => failure.PropertyName == "expiryYear");
    }

    [Theory]
    [InlineData(5, 2030)]
    [InlineData(12, 2029)]
    public void Validate_RejectsExpiredCards(int month, int year)
    {
        // Arrange
        var request = new PostPaymentRequest(
            "2222405343248877", month, year, "GBP", 1050, "123");

        // Act
        var errors = _validator.TestValidate(request);

        // Assert
        Assert.Contains(errors.Errors, failure => failure.PropertyName == "expiryYear");
    }

    [Theory]
    [InlineData(0, 2031)]
    [InlineData(13, 2031)]
    [InlineData(7, 0)]
    [InlineData(7, -1)]
    [InlineData(13, int.MaxValue)]
    public void Validate_RejectsInvalidExpiryValuesWithoutThrowing(int month, int year)
    {
        // Arrange
        var request = new PostPaymentRequest(
            "2222405343248877", month, year, "GBP", 1050, "123");

        // Act
        var exception = Record.Exception(() => _validator.TestValidate(request));
        var errors = _validator.TestValidate(request);

        // Assert
        Assert.Null(exception);
        Assert.NotEmpty(errors.Errors);
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void Validate_AcceptsSupportedCurrencies(string currency)
    {
        // Arrange
        var request = new PostPaymentRequest(
            "2222405343248877", 7, 2031, currency, 1050, "123");

        // Act
        var errors = _validator.TestValidate(request);

        // Assert
        Assert.DoesNotContain(errors.Errors, failure => failure.PropertyName == "currency");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("gbp")]
    [InlineData("JPY")]
    [InlineData("US")]
    [InlineData("USDD")]
    public void Validate_RejectsInvalidCurrencies(string? currency)
    {
        // Arrange
        var request = new PostPaymentRequest(
            "2222405343248877", 7, 2031, currency, 1050, "123");

        // Act
        var errors = _validator.TestValidate(request);

        // Assert
        Assert.Contains(errors.Errors, failure => failure.PropertyName == "currency");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("0123")]
    public void Validate_AcceptsValidCvvIncludingLeadingZeroes(string cvv)
    {
        // Arrange
        var request = new PostPaymentRequest(
            "2222405343248877", 7, 2031, "GBP", 1050, cvv);

        // Act
        var errors = _validator.TestValidate(request);

        // Assert
        Assert.DoesNotContain(errors.Errors, failure => failure.PropertyName == "cvv");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("12a")]
    [InlineData("123\n")]
    [InlineData("١٢٣")]
    public void Validate_RejectsInvalidCvv(string? cvv)
    {
        // Arrange
        var request = new PostPaymentRequest(
            "2222405343248877", 7, 2031, "GBP", 1050, cvv);

        // Act
        var errors = _validator.TestValidate(request);

        // Assert
        Assert.Contains(errors.Errors, failure => failure.PropertyName == "cvv");
    }

    [Fact]
    public void Validate_AcceptsPositiveAmountBoundary()
    {
        // Arrange
        var request = new PostPaymentRequest(
            "2222405343248877", 7, 2031, "GBP", int.MaxValue, "123");

        // Act
        var errors = _validator.TestValidate(request);

        // Assert
        Assert.DoesNotContain(errors.Errors, failure => failure.PropertyName == "amount");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Validate_RejectsNonPositiveAmounts(int amount)
    {
        // Arrange
        var request = new PostPaymentRequest(
            "2222405343248877", 7, 2031, "GBP", amount, "123");

        // Act
        var errors = _validator.TestValidate(request);

        // Assert
        Assert.Contains(errors.Errors, failure => failure.PropertyName == "amount");
    }

    [Fact]
    public void Validate_HandlesDecemberToJanuaryBoundary()
    {
        // Arrange
        _timeProviderMock
            .Setup(provider => provider.GetUtcNow())
            .Returns(new DateTimeOffset(2030, 12, 31, 23, 59, 59, TimeSpan.Zero));

        // Act
        var currentMonth = _validator.TestValidate(new PostPaymentRequest(
            "2222405343248877", 12, 2030, "GBP", 1050, "123"));
        var nextMonth = _validator.TestValidate(new PostPaymentRequest(
            "2222405343248877", 1, 2031, "GBP", 1050, "123"));
        var previousMonth = _validator.TestValidate(new PostPaymentRequest(
            "2222405343248877", 11, 2030, "GBP", 1050, "123"));

        // Assert
        Assert.DoesNotContain(
            currentMonth.Errors,
            failure => failure.PropertyName == "expiryYear");
        Assert.DoesNotContain(
            nextMonth.Errors,
            failure => failure.PropertyName == "expiryYear");
        Assert.Contains(
            previousMonth.Errors,
            failure => failure.PropertyName == "expiryYear");
    }

    [Fact]
    public void Validate_ReportsAllIndependentInvalidFields()
    {
        // Arrange
        var request = new PostPaymentRequest(null, 13, 0, null, 0, null);

        // Act
        var errors = _validator.TestValidate(request);

        // Assert
        Assert.Equal(6, errors.Errors.Count);
    }
}
