using DokPortal.Infrastructure.Auth;

namespace DokPortal.Api.Security;

public static class SecurityMiddleware
{
    /// <summary>
    /// Nagłówki bezpieczeństwa dla każdej odpowiedzi API: przeglądarka nie zgaduje typu pliku, nie osadza odpowiedzi
    /// w ramce, nie wykonuje w niej skryptów, nie wysyła adresu dalej i nie zapisuje danych osobowych w pamięci podręcznej
    /// (chyba że endpoint sam ustawił Cache-Control, np. zdjęcia).
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            headers["Cross-Origin-Resource-Policy"] = "cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            if (context.Request.IsHttps) headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            if (!headers.ContainsKey("Cache-Control") && context.Request.Path.StartsWithSegments("/api")) headers["Cache-Control"] = "no-store";
            headers.Remove("Server");
            return Task.CompletedTask;
        });
        await next();
    });

    /// <summary>
    /// Hasło tymczasowe (nadane przez administratora) otwiera tylko zmianę hasła: do czasu ustawienia własnego
    /// reszta API odpowiada 403. Sama blokada w przeglądarce nie wystarcza – token działa też poza nią.
    /// </summary>
    public static IApplicationBuilder UseMustChangePasswordGate(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (context.User.HasClaim(c => c.Type == JwtTokenGenerator.MustChangePasswordClaim)
            && context.Request.Path.StartsWithSegments("/api")
            && !context.Request.Path.StartsWithSegments("/api/auth"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "Najpierw ustaw własne hasło (Moje konto → Hasło)." });
            return;
        }
        await next();
    });
}
