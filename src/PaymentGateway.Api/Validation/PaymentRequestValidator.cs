using PaymentGateway.Api.Contracts;

namespace PaymentGateway.Api.Validation;

public sealed class PaymentRequestValidator(TimeProvider timeProvider)
{
    private static readonly HashSet<string> SupportedCurrencies =
        new(StringComparer.Ordinal) { "GBP", "USD", "EUR" };

    public IReadOnlyDictionary<string, string[]> Validate(PostPaymentRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        ValidateCardNumber(request.CardNumber, errors);
        ValidateExpiry(request.ExpiryMonth, request.ExpiryYear, errors);
        ValidateCurrency(request.Currency, errors);

        if (request.Amount <= 0)
        {
            Add(errors, "amount", "Amount must be greater than zero.");
        }

        ValidateCvv(request.Cvv, errors);

        return errors.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray());
    }

    private static void ValidateCardNumber(string? cardNumber, IDictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
        {
            Add(errors, "cardNumber", "Card number is required.");
            return;
        }

        if (cardNumber.Length is < 14 or > 19)
        {
            Add(errors, "cardNumber", "Card number must be between 14 and 19 characters.");
        }

        if (!ContainsOnlyAsciiDigits(cardNumber))
        {
            Add(errors, "cardNumber", "Card number must contain only numeric characters.");
        }
    }

    private void ValidateExpiry(int expiryMonth, int expiryYear, IDictionary<string, List<string>> errors)
    {
        if (expiryMonth is < 1 or > 12)
        {
            Add(errors, "expiryMonth", "Expiry month must be between 1 and 12.");
        }

        if (expiryYear <= 0)
        {
            Add(errors, "expiryYear", "Expiry year is required.");
            return;
        }

        if (expiryMonth is < 1 or > 12)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        if (expiryYear < now.Year || expiryYear == now.Year && expiryMonth < now.Month)
        {
            Add(errors, "expiryYear", "Card has expired.");
        }
    }

    private static void ValidateCurrency(string? currency, IDictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            Add(errors, "currency", "Currency is required.");
            return;
        }

        if (currency.Length != 3)
        {
            Add(errors, "currency", "Currency must be three characters.");
        }

        if (!SupportedCurrencies.Contains(currency))
        {
            Add(errors, "currency", "Currency must be one of GBP, USD, or EUR.");
        }
    }

    private static void ValidateCvv(string? cvv, IDictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(cvv))
        {
            Add(errors, "cvv", "CVV is required.");
            return;
        }

        if (cvv.Length is < 3 or > 4)
        {
            Add(errors, "cvv", "CVV must be between 3 and 4 characters.");
        }

        if (!ContainsOnlyAsciiDigits(cvv))
        {
            Add(errors, "cvv", "CVV must contain only numeric characters.");
        }
    }

    private static bool ContainsOnlyAsciiDigits(string value) =>
        value.All(character => character is >= '0' and <= '9');

    private static void Add(IDictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var fieldErrors))
        {
            fieldErrors = [];
            errors[field] = fieldErrors;
        }

        fieldErrors.Add(message);
    }
}