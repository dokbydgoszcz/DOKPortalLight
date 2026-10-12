using DokPortal.Api.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>
/// Budząca się darmowa baza daje przejściowy błąd. Musi wracać jako 503 (spróbuj ponownie), a nie 400 – inaczej
/// workflowy przypomnień uznają go za ostateczny i nie ponawiają (tak przepadały przypomnienia 5–12.10.2026).
/// </summary>
public class TransientDatabaseErrorTests
{
    private static async Task<int> StatusForAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance).TryHandleAsync(context, exception, default);
        return context.Response.StatusCode;
    }

    [Fact]
    public async Task TransientDatabaseFailures_AreServiceUnavailable_ButBusinessRulesStayBadRequest()
    {
        var efTransient = new InvalidOperationException(
            "An exception has been raised that is likely due to a transient failure. Consider enabling transient error resiliency by adding 'EnableRetryOnFailure' to the 'UseSqlServer' call.",
            new TimeoutException());

        Assert.Equal(503, await StatusForAsync(efTransient));
        Assert.Equal(503, await StatusForAsync(new RetryLimitExceededException("limit", new TimeoutException())));
        Assert.Equal(503, await StatusForAsync(new TimeoutException()));
        Assert.Equal(400, await StatusForAsync(new InvalidOperationException("Nie znaleziono osoby.")));
    }
}
