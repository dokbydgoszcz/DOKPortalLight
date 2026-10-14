using DokPortal.Application.Common;
using DokPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DokPortal.Infrastructure.Persistence;

namespace DokPortal.Infrastructure.Services;

/// <summary>
/// Proboszcz wskazany już po skierowaniu katechistów (giełda parafii) dostaje ich dane kontaktowe, a każdy z tych
/// katechistów – dane proboszcza. Wcześniej katechista dostawał tylko obietnicę „prześlemy po wskazaniu proboszcza”.
/// </summary>
public static class PastorReferralNotifier
{
    /// <summary>Skierowania starsze niż rok uznajemy za historię – nie piszemy o nich.</summary>
    private static readonly TimeSpan Window = TimeSpan.FromDays(365);

    public static async Task NotifyAsync(AppDbContext db, IEmailSender? sender, ILogger? logger, Guid parishId, Guid pastorPersonId, CancellationToken ct)
    {
        if (sender is null) return;
        var since = DateTime.UtcNow - Window;
        var referrals = await db.ParishNeedAssignments.AsNoTracking()
            .Where(a => a.ParishNeed!.ParishId == parishId && a.AssignedAtUtc >= since)
            .Select(a => new { a.Person, a.ParishNeed!.Description })
            .ToListAsync(ct);
        if (referrals.Count == 0) return;

        var pastor = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pastorPersonId, ct);
        var parish = await db.Parishes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == parishId, ct);
        if (pastor is null || parish is null) return;

        var subject = $"Katechiści skierowani do parafii {parish.Name}";
        var lines = referrals.Where(r => r.Person is not null)
            .Select(r => $"• {r.Person!.FullName} (zapotrzebowanie: {r.Description})\n{Contact(r.Person)}");
        await TrySendAsync(sender, logger, pastor.Email, subject,
            $"Szczęść Boże,\n\nw DOK Portal wskazano Księdza jako proboszcza parafii „{parish.Name}”. " +
            $"Do tej parafii skierowano wcześniej katechistów:\n\n{string.Join("\n\n", lines)}\n\nProsimy o bezpośredni kontakt.", ct);

        foreach (var catechist in referrals.Select(r => r.Person).Where(p => p is not null).DistinctBy(p => p!.Id))
        {
            await TrySendAsync(sender, logger, catechist!.Email, $"Proboszcz parafii {parish.Name}",
                $"Szczęść Boże,\n\nparafia „{parish.Name}”, do której zostałeś(-aś) skierowany(-a), ma już wskazanego proboszcza.\n" +
                $"Dane kontaktowe proboszcza ({pastor.FullName}):\n{Contact(pastor)}\n\nProsimy o bezpośredni kontakt z parafią.", ct);
        }
    }

    private static string Contact(Person p) =>
        $"- e-mail: {(string.IsNullOrWhiteSpace(p.Email) ? "brak" : p.Email)}\n- telefon: {(string.IsNullOrWhiteSpace(p.Phone) ? "brak" : p.Phone)}";

    private static async Task TrySendAsync(IEmailSender sender, ILogger? logger, string? to, string subject, string body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(to)) return;
        try
        {
            await sender.SendAsync(to, subject, body, ct);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Nie udało się wysłać powiadomienia o proboszczu ({Subject})", subject);
        }
    }
}
