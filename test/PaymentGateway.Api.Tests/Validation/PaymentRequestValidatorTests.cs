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
        var errors = _validator.Validate(TestRequests.Valid(cardNumber: cardNumber));

        Assert.DoesNotContain("cardNumber", errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("             ")]
    [InlineData("1234567890123")]
    [InlineData("12345678901234567890")]
    [InlineData("1234567890123a")]
    [InlineData("1234567890123-")]
    [InlineData("١٢٣٤٥٦٧٨٩٠١٢٣٤")]
    public void RejectsInvalidCardNumbers(string? cardNumber)
    {
        var errors = _validator.Validate(TestRequests.Valid(cardNumber: cardNumber!));

        Assert.Contains("cardNumber", errors.Keys);
    }

    [Theory]
    [InlineData(6, 2030)]
    [InlineData(7, 2030)]
    [InlineData(1, 2031)]
    public void AcceptsCurrentAndFutureExpiryMonths(int month, int year)
    {
        var errors = _validator.Validate(TestRequests.Valid(expiryMonth: month, expiryYear: year));

        Assert.DoesNotContain("expiryMonth", errors.Keys);
        Assert.DoesNotContain("expiryYear", errors.Keys);
    }

    [Theory]
    [InlineData(5, 2030)]
    [InlineData(12, 2029)]
    public void RejectsExpiredCards(int month, int year)
    {
        var errors = _validator.Validate(TestRequests.Valid(expiryMonth: month, expiryYear: year));

        Assert.Contains("expiryYear", errors.Keys);
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
            _validator.Validate(TestRequests.Valid(expiryMonth: month, expiryYear: year)));

        Assert.Null(exception);
        var errors = _validator.Validate(TestRequests.Valid(expiryMonth: month, expiryYear: year));
        Assert.NotEmpty(errors);
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void AcceptsSupportedCurrencies(string currency)
    {
        var errors = _validator.Validate(TestRequests.Valid(currency: currency));

        Assert.DoesNotContain("currency", errors.Keys);
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
        var errors = _validator.Validate(TestRequests.Valid(currency: currency!));

        Assert.Contains("currency", errors.Keys);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("0123")]
    public void AcceptsValidCvvIncludingLeadingZeroes(string cvv)
    {
        var errors = _validator.Validate(TestRequests.Valid(cvv: cvv));

        Assert.DoesNotContain("cvv", errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("12a")]
    [InlineData("١٢٣")]
    public void RejectsInvalidCvv(string? cvv)
    {
        var errors = _validator.Validate(TestRequests.Valid(cvv: cvv!));

        Assert.Contains("cvv", errors.Keys);
    }

    [Fact]
    public void AcceptsPositiveAmountBoundary()
    {
        var errors = _validator.Validate(TestRequests.Valid(amount: int.MaxValue));

        Assert.DoesNotContain("amount", errors.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void RejectsNonPositiveAmounts(int amount)
    {
        var errors = _validator.Validate(TestRequests.Valid(amount: amount));

        Assert.Contains("amount", errors.Keys);
    }

    [Fact]
    public void HandlesDecemberToJanuaryBoundary()
    {
        var validator = new PaymentRequestValidator(
            new StubTimeProvider(new DateTimeOffset(2030, 12, 31, 23, 59, 59, TimeSpan.Zero)));

        Assert.DoesNotContain("expiryYear", validator.Validate(TestRequests.Valid(expiryMonth: 12, expiryYear: 2030)).Keys);
        Assert.DoesNotContain("expiryYear", validator.Validate(TestRequests.Valid(expiryMonth: 1, expiryYear: 2031)).Keys);
        Assert.Contains("expiryYear", validator.Validate(TestRequests.Valid(expiryMonth: 11, expiryYear: 2030)).Keys);
    }

    [Fact]
    public void ReportsAllIndependentInvalidFields()
    {
        var request = new PostPaymentRequest(null, 13, 0, null, 0, null);

        var errors = _validator.Validate(request);

        Assert.Equal(6, errors.Count);
    }
}