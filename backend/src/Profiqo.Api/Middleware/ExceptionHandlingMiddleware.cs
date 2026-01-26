using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc;

using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Common;

namespace Profiqo.Api.Middleware;

internal sealed class ExceptionHandlingMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await WriteProblemDetails(context, ex);
        }
    }

    private static async Task WriteProblemDetails(HttpContext ctx, Exception ex)
    {
        var (status, code, title, errors) = ex switch
        {
            AppValidationException v => (HttpStatusCode.BadRequest, v.Code, "Validation error", v.Errors),
            NotFoundException nf => (HttpStatusCode.NotFound, nf.Code, nf.Message, null),
            UnauthorizedException un => (HttpStatusCode.Unauthorized, un.Code, un.Message, null),
            ForbiddenException fb => (HttpStatusCode.Forbidden, fb.Code, fb.Message, null),
            ConflictException cf => (HttpStatusCode.Conflict, cf.Code, cf.Message, null),
            BusinessRuleViolationException br => (HttpStatusCode.UnprocessableEntity, br.Code, br.Message, null),
            DomainException de => (HttpStatusCode.UnprocessableEntity, "domain_error", de.Message, null),
            _ => (HttpStatusCode.InternalServerError, "internal_error", "Unexpected error", null)
        };

        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Type = code,
            Detail = status == HttpStatusCode.InternalServerError ? null : ex.Message,
            Instance = ctx.Request.Path
        };

        if (errors is not null)
            problem.Extensions["errors"] = errors;

        ctx.Response.StatusCode = (int)status;
        ctx.Response.ContentType = "application/problem+json";

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
