using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.AuditLog;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DokPortal.Api.Filters;

/// <summary>
/// Zapisuje w dzienniku zmian udaną operację (zmianę danych, wgranie lub pobranie pliku). Opis obiektu bierze z odpowiedzi
/// (imię i nazwisko, nazwa, tytuł, nazwa pliku), a gdy jej brak (np. usunięcie) – z identyfikatorów w adresie.
/// Nieudane operacje (4xx/5xx, wyjątki) nie są zapisywane – błędy uprawnień zapisują się osobno.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AuditAttribute : ActionFilterAttribute
{
    private static readonly string[] DescriptionProperties = { "PersonFullName", "FullName", "Name", "Title", "Description", "Email" };

    public AuditAttribute(string action) => Action = action;

    public string Action { get; }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is not null && !executed.ExceptionHandled) return;
        if (!IsSuccess(executed.Result)) return;

        var user = context.HttpContext.User;
        var userId = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId is null) return;
        var email = user.FindFirst(JwtRegisteredClaimNames.Email)?.Value ?? "";
        var audit = context.HttpContext.RequestServices.GetRequiredService<IAuditLogService>();
        await audit.LogAsync(userId, email, Action, Describe(executed.Result, context), AuditResult.Allowed, context.HttpContext.RequestAborted);
    }

    private static bool IsSuccess(IActionResult? result) => result switch
    {
        ObjectResult o => o.StatusCode is null or >= 200 and < 300,
        StatusCodeResult s => s.StatusCode is >= 200 and < 300,
        FileResult => true,
        _ => false
    };

    /// <summary>„Nazwa [id=…, caseId=…]” – czytelna nazwa (gdy jest) i identyfikatory z adresu, żeby wpis dało się powiązać z obiektem.</summary>
    private static string Describe(IActionResult? result, ActionExecutingContext context)
    {
        var ids = string.Join(", ", context.RouteData.Values
            .Where(v => v.Key is not ("controller" or "action") && v.Value is not null)
            .Select(v => $"{v.Key}={v.Value}"));
        var label = Label(result);
        if (label is null) return ids;
        if (label.Length > 250) label = label[..250];
        return ids.Length == 0 ? label : $"{label} [{ids}]";
    }

    private static string? Label(IActionResult? result)
    {
        if (result is FileResult file && !string.IsNullOrWhiteSpace(file.FileDownloadName)) return file.FileDownloadName;
        if (result is not ObjectResult { Value: { } value }) return null;
        foreach (var name in DescriptionProperties)
        {
            if (value.GetType().GetProperty(name)?.GetValue(value) is string text && !string.IsNullOrWhiteSpace(text)) return text;
        }
        return null;
    }
}
