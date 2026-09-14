using System.Net.Http.Json;

using PaymentGateway.Api.Enums;

namespace PaymentGateway.Api.Clients;

public sealed class AcquiringBankClient(HttpClient httpClient) : IAcquiringBankClient
{
    public async Task<BankPaymentResponse> ProcessPaymentAsync(
        BankPaymentRequest request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await httpClient.PostAsJsonAsync("payments", request, cancellationToken);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AcquiringBankException(
                AcquiringBankFailure.Timeout,
                "The acquiring bank did not respond before the configured timeout.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new AcquiringBankException(
                AcquiringBankFailure.Unavailable,
                "The acquiring bank could not be reached.",
                exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new AcquiringBankException(
                    AcquiringBankFailure.Unavailable,
                    $"The acquiring bank returned HTTP {(int)response.StatusCode}.");
            }

            BankPaymentResponse? bankResponse;
            try
            {
                bankResponse = await response.Content.ReadFromJsonAsync<BankPaymentResponse>(cancellationToken);
            }
            catch (Exception exception) when (exception is System.Text.Json.JsonException or NotSupportedException)
            {
                throw new AcquiringBankException(
                    AcquiringBankFailure.InvalidResponse,
                    "The acquiring bank returned malformed JSON.",
                    exception);
            }

            if (bankResponse?.Authorized is null || bankResponse.AuthorizationCode is null ||
                bankResponse.Authorized.Value && string.IsNullOrWhiteSpace(bankResponse.AuthorizationCode))
            {
                throw new AcquiringBankException(
                    AcquiringBankFailure.InvalidResponse,
                    "The acquiring bank response was incomplete.");
            }

            return bankResponse;
        }
    }
}