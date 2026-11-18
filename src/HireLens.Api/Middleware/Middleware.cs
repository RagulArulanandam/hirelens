using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Serilog.Context;

namespace HireLens.Api.Middleware;

public class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string Header = "X-Correlation-ID";

    public async Task Invoke(HttpContext ctx)
    {
        var id = ctx.Request.Headers[Header].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64) id = Guid.NewGuid().ToString("N");
        ctx.Items[Header] = id;
        ctx.Response.OnStarting(() => { ctx.Response.Headers[Header] = id; return Task.CompletedTask; });
        using (LogContext.PushProperty("CorrelationId", id)) await next(ctx);
    }
}

public class RequestTimingMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext ctx)
    {
        var sw = Stopwatch.StartNew();
        ctx.Response.OnStarting(() => { ctx.Response.Headers["X-Response-Time-ms"] = sw.ElapsedMilliseconds.ToString(); return Task.CompletedTask; });
        await next(ctx);
    }
}

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log)
{
    public async Task Invoke(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (Exception ex) when (!ctx.Response.HasStarted)
        {
            var (status, title) = ex switch
            {
                ValidationException => (400, "Validation failed"),
                KeyNotFoundException => (404, "Not found"),
                UnauthorizedAccessException => (403, "Forbidden"),
                BadHttpRequestException => (400, "Bad request"),
                _ => (500, "Unexpected error")
            };
            if (status == 500) log.LogError(ex, "Unhandled exception");
            else log.LogWarning("Handled {Type}: {Message}", ex.GetType().Name, ex.Message);

            var pd = new ProblemDetails { Status = status, Title = title, Instance = ctx.Request.Path };
            pd.Extensions["correlationId"] = ctx.Items[CorrelationIdMiddleware.Header];
            if (ex is ValidationException ve)
                pd.Extensions["errors"] = ve.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            else if (status != 500) pd.Detail = ex.Message;

            ctx.Response.Clear();
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(pd, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
        }
    }
}
