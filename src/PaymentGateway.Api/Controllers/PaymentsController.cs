using FluentValidation;

using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(
    IValidator<PostPaymentRequest> validator,
    IPaymentService paymentService,
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
            logger.LogDebug("Retrieved payment {PaymentId}", id);
            return Ok(new PaymentResponse(
                payment.Id,
                payment.Status,
                payment.CardNumberLastFour,
                payment.ExpiryMonth,
                payment.ExpiryYear,
                payment.Currency,
                payment.Amount));
        }

        logger.LogInformation("Payment {PaymentId} was not found", id);
        var problem = new ProblemDetails
        {
            Type = "https://httpstatuses.com/404",
            Title = "Payment was not found",
            Status = StatusCodes.Status404NotFound,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return NotFound(problem);
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
            logger.LogInformation(
                "Payment request rejected with {ValidationErrorCount} validation errors on fields {InvalidFields}",
                1,
                "request");
            var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["request"] = ["A payment request body is required."]
            })
            {
                Type = "https://httpstatuses.com/400",
                Title = "Payment request was rejected",
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["paymentStatus"] = "Rejected";
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
            return BadRequest(problem);
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray(),
                    StringComparer.Ordinal);
            logger.LogInformation(
                "Payment request rejected with {ValidationErrorCount} validation errors on fields {InvalidFields}",
                validationResult.Errors.Count,
                string.Join(',', errors.Keys.Order(StringComparer.Ordinal)));
            var problem = new ValidationProblemDetails(errors)
            {
                Type = "https://httpstatuses.com/400",
                Title = "Payment request was rejected",
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["paymentStatus"] = "Rejected";
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
            return BadRequest(problem);
        }

        PaymentModel payment;
        try
        {
            payment = await paymentService.ProcessAsync(request, cancellationToken);
        }
        catch (AcquiringBankException)
        {
            var problem = new ProblemDetails
            {
                Type = "https://httpstatuses.com/502",
                Title = "The acquiring bank is unavailable",
                Status = StatusCodes.Status502BadGateway,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
            return StatusCode(StatusCodes.Status502BadGateway, problem);
        }

        return Ok(new PaymentResponse(
            payment.Id,
            payment.Status,
            payment.CardNumberLastFour,
            payment.ExpiryMonth,
            payment.ExpiryYear,
            payment.Currency,
            payment.Amount));
    }
}
