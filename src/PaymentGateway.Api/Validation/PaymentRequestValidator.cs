using FluentValidation;

using PaymentGateway.Api.Contracts;

namespace PaymentGateway.Api.Validation;

public sealed class PaymentRequestValidator : AbstractValidator<PostPaymentRequest>
{
    private static readonly HashSet<string> SupportedCurrencies =
        new(StringComparer.Ordinal) { "GBP", "USD", "EUR" };

    private readonly TimeProvider _timeProvider;

    public PaymentRequestValidator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.CardNumber)
            .NotEmpty()
            .WithMessage("Card number is required.")
            .Length(14, 19)
            .WithMessage("Card number must be between 14 and 19 characters.")
            .Must(ContainOnlyAsciiDigits)
            .WithMessage("Card number must contain only numeric characters.")
            .OverridePropertyName("cardNumber");

        RuleFor(request => request.ExpiryMonth)
            .InclusiveBetween(1, 12)
            .WithMessage("Expiry month must be between 1 and 12.")
            .OverridePropertyName("expiryMonth");

        RuleFor(request => request.ExpiryYear)
            .GreaterThan(0)
            .WithMessage("Expiry year is required.")
            .Must(BeCurrentOrFutureExpiry)
            .When(
                request => request.ExpiryMonth is >= 1 and <= 12 && request.ExpiryYear > 0,
                ApplyConditionTo.CurrentValidator)
            .WithMessage("Card has expired.")
            .OverridePropertyName("expiryYear");

        RuleFor(request => request.Currency)
            .NotEmpty()
            .WithMessage("Currency is required.")
            .Length(3)
            .WithMessage("Currency must be three characters.")
            .Must(currency => SupportedCurrencies.Contains(currency!))
            .WithMessage("Currency must be one of GBP, USD, or EUR.")
            .OverridePropertyName("currency");

        RuleFor(request => request.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero.")
            .OverridePropertyName("amount");

        RuleFor(request => request.Cvv)
            .NotEmpty()
            .WithMessage("CVV is required.")
            .Length(3, 4)
            .WithMessage("CVV must be between 3 and 4 characters.")
            .Must(ContainOnlyAsciiDigits)
            .WithMessage("CVV must contain only numeric characters.")
            .OverridePropertyName("cvv");
    }

    private bool BeCurrentOrFutureExpiry(PostPaymentRequest request, int expiryYear)
    {
        var now = _timeProvider.GetUtcNow();
        return expiryYear > now.Year || expiryYear == now.Year && request.ExpiryMonth >= now.Month;
    }

    private static bool ContainOnlyAsciiDigits(string? value) =>
        value is not null && value.All(character => character is >= '0' and <= '9');
}
