using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Observability;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(
    PaymentRequestValidator validator,
    PaymentService paymentService,
    PaymentTelemetry telemetry,
    ILogger<PaymentsController> logger) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public ActionResult<PaymentResponse> GetPayment(Guid id)
    {
        var payment = paymentService.Get(id);
        if (payment is not null)
        {
            return Ok(payment);
        }

        return NotFound(ProblemResponses.Create(
            HttpContext,
            StatusCodes.Status404NotFound,
            "Payment was not found",
            "https://httpstatuses.com/404"));
    }

    [HttpPost]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PaymentResponse>> PostPayment(
        [FromBody] PostPaymentRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            telemetry.RecordRejected();
            return BadRequest(ProblemResponses.Rejected(
                HttpContext,
                new Dictionary<string, string[]>
                {
                    ["request"] = ["A payment request body is required."]
                }));
        }

        var errors = validator.Validate(request);
        if (errors.Count > 0)
        {
            telemetry.RecordRejected();
            logger.LogInformation("Payment request rejected with {ValidationErrorCount} validation errors", errors.Count);
            return BadRequest(ProblemResponses.Rejected(
                HttpContext,
                errors.ToDictionary(entry => entry.Key, entry => entry.Value)));
        }

        var payment = await paymentService.ProcessAsync(request, cancellationToken);
        return Ok(payment);
    }
}