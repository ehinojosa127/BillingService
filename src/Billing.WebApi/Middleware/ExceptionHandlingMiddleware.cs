using Billing.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Billing.WebApi.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var (status, code, title, errors) = Map(ex);
            if (status >= 500)
            {
                logger.LogError(ex, "Unhandled error. CorrelationId={CorrelationId} Code={Code}", context.TraceIdentifier, code);
            }
            else
            {
                logger.LogWarning(ex, "Request failed. CorrelationId={CorrelationId} Code={Code}", context.TraceIdentifier, code);
            }

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Type = code,
                Detail = ex is BillingApplicationException ? ex.Message : title,
                Instance = context.Request.Path
            };
            if (errors is not null)
            {
                problem.Extensions["errors"] = errors;
            }

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem);
        }
    }

    private static (int Status, string Code, string Title, IReadOnlyList<string>? Errors) Map(Exception exception) =>
        exception switch
        {
            ValidationException validation => (StatusCodes.Status400BadRequest, validation.Code, "The request is invalid.", validation.Errors),
            NotFoundException notFound => (StatusCodes.Status404NotFound, notFound.Code, notFound.Message, null),
            ConflictException conflict => (StatusCodes.Status409Conflict, conflict.Code, conflict.Message, null),
            Billing.Domain.Exceptions.BusinessRuleException business => (StatusCodes.Status422UnprocessableEntity, business.Code, business.Message, null),
            Billing.Domain.Exceptions.InvalidStatusTransitionException transition => (StatusCodes.Status409Conflict, "INVALID_STATUS_TRANSITION", transition.Message, null),
            SunatRejectionException rejection => (StatusCodes.Status422UnprocessableEntity, rejection.Code, rejection.Message, null),
            SunatUnavailableException unavailable => (StatusCodes.Status503ServiceUnavailable, unavailable.Code, "SUNAT is currently unavailable.", null),
            TransientCommunicationException transient => (StatusCodes.Status503ServiceUnavailable, transient.Code, "A temporary communication error occurred.", null),
            PersistenceException => (StatusCodes.Status500InternalServerError, "PERSISTENCE_ERROR", "A persistence error occurred.", null),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "An internal error occurred.", null)
        };
}
