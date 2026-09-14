using FluentValidation.TestHelper;

using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Tests.TestDoubles;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Tests.Validation;

public sealed class PaymentRequestValidatorTests
{
    private static readonly DateTimeOffset Now = new(2030, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly PaymentRequestValidator _validator = new(new StubTimeProvider(Now));

    [Theory]
    [InlineData("12345678901234")]
    [InlineData("1234567890123456789")]
    [InlineData("00000000000000")]
    public void AcceptsCardNumberBoundaryValues(string cardNumber)
    {
        var errors = _validator.TestValidate(TestRequests.Valid(cardNumber: cardNumber));

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
    [InlineData("١٢٣٤٥٦٧٨٩٠١٢٣٤")]
    public void RejectsInvalidCardNumbers(string? cardNumber)
    {
        var errors = _validator.TestValidate(TestRequests.Valid(cardNumber: cardNumber));

        Assert.Contains(errors.Errors, failure => failure.PropertyName == "cardNumber");
    }

    [Fact]
    public void ReturnsStablePropertyNameAndMessage()
    {
        var result = _validator.TestValidate(TestRequests.Valid(cardNumber: "123"));

        var failure = Assert.Single(result.Errors, error => error.PropertyName == "cardNumber");
        Assert.Equal("Card number must be between 14 and 19 characters.", failure.ErrorMessage);
    }

    [Fact]
    public void StopsEvaluatingAPropertyAfterItsFirstFailure()
    {
        var result = _validator.TestValidate(TestRequests.Valid(cardNumber: "abc"));

        var failure = Assert.Single(result.Errors, error => error.PropertyName == "cardNumber");
        Assert.Equal("Card number must be between 14 and 19 characters.", failure.ErrorMessage);
    }

    [Theory]
    [InlineData(6, 2030)]
    [InlineData(7, 2030)]
    [InlineData(1, 2031)]
    public void AcceptsCurrentAndFutureExpiryMonths(int month, int year)
    {
        var errors = _validator.TestValidate(TestRequests.Valid(expiryMonth: month, expiryYear: year));

        Assert.DoesNotContain(errors.Errors, failure => failure.PropertyName == "expiryMonth");
        Assert.DoesNotContain(errors.Errors, failure => failure.PropertyName == "expiryYear");
    }

    [Theory]
    [InlineData(5, 2030)]
    [InlineData(12, 2029)]
    public void RejectsExpiredCards(int month, int year)
    {
        var errors = _validator.TestValidate(TestRequests.Valid(expiryMonth: month, expiryYear: year));

        Assert.Contains(errors.Errors, failure => failure.PropertyName == "expiryYear");
    }

    [Theory]
    [InlineData(0, 2031)]
    [InlineData(13, 2031)]
    [InlineData(7, 0)]
    [InlineData(7, -1)]
    [InlineData(13, int.MaxValue)]
    public void RejectsInvalidExpiryValuesWithoutThrowing(int month, int year)
    {
        var exception = Record.Exception(() =>
            _validator.TestValidate(TestRequests.Valid(expiryMonth: month, expiryYear: year)));

        Assert.Null(exception);
        var errors = _validator.TestValidate(TestRequests.Valid(expiryMonth: month, expiryYear: year));
        Assert.NotEmpty(errors.Errors);
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void AcceptsSupportedCurrencies(string currency)
    {
        var errors = _validator.TestValidate(TestRequests.Valid(currency: currency));

        Assert.DoesNotContain(errors.Errors, failure => failure.PropertyName == "currency");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("gbp")]
    [InlineData("JPY")]
    [InlineData("US")]
    [InlineData("USDD")]
    public void RejectsInvalidCurrencies(string? currency)
    {
        var errors = _validator.TestValidate(TestRequests.Valid(currency: currency));

        Assert.Contains(errors.Errors, failure => failure.PropertyName == "currency");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("0123")]
    public void AcceptsValidCvvIncludingLeadingZeroes(string cvv)
    {
        var errors = _validator.TestValidate(TestRequests.Valid(cvv: cvv));

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
    public void RejectsInvalidCvv(string? cvv)
    {
        var errors = _validator.TestValidate(TestRequests.Valid(cvv: cvv));

        Assert.Contains(errors.Errors, failure => failure.PropertyName == "cvv");
    }

    [Fact]
    public void AcceptsPositiveAmountBoundary()
    {
        var errors = _validator.TestValidate(TestRequests.Valid(amount: int.MaxValue));

        Assert.DoesNotContain(errors.Errors, failure => failure.PropertyName == "amount");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void RejectsNonPositiveAmounts(int amount)
    {
        var errors = _validator.TestValidate(TestRequests.Valid(amount: amount));

        Assert.Contains(errors.Errors, failure => failure.PropertyName == "amount");
    }

    [Fact]
    public void HandlesDecemberToJanuaryBoundary()
    {
        var validator = new PaymentRequestValidator(
            new StubTimeProvider(new DateTimeOffset(2030, 12, 31, 23, 59, 59, TimeSpan.Zero)));

        Assert.DoesNotContain(
            validator.TestValidate(TestRequests.Valid(expiryMonth: 12, expiryYear: 2030)).Errors,
            failure => failure.PropertyName == "expiryYear");
        Assert.DoesNotContain(
            validator.TestValidate(TestRequests.Valid(expiryMonth: 1, expiryYear: 2031)).Errors,
            failure => failure.PropertyName == "expiryYear");
        Assert.Contains(
            validator.TestValidate(TestRequests.Valid(expiryMonth: 11, expiryYear: 2030)).Errors,
            failure => failure.PropertyName == "expiryYear");
    }

    [Fact]
    public void ReportsAllIndependentInvalidFields()
    {
        var request = new PostPaymentRequest(null, 13, 0, null, 0, null);

        var errors = _validator.TestValidate(request);

        Assert.Equal(6, errors.Errors.Count);
    }
}
