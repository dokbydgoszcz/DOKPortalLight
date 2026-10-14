using System.Security.Cryptography;
using System.Text;
using DokPortal.Application.DokCases;
using DokPortal.Application.Meetings;
using DokPortal.Application.Permissions;
using DokPortal.Domain.Constants;
using DokPortal.Infrastructure.Identity;
using DokPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class CalendarFeedService : ICalendarFeedService
{
    /// <summary>Kalendarz obejmuje spotkania od 3 miesięcy wstecz (starsze nie są potrzebne w telefonie).</summary>
    private const int PastDays = 90;

    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly IPermissionService _permissions;
    private readonly TimeProvider _time;

    public CalendarFeedService(AppDbContext db, UserManager<AppUser> userManager, IPermissionService permissions, TimeProvider? time = null)
    {
        _db = db;
        _userManager = userManager;
        _permissions = permissions;
        _time = time ?? TimeProvider.System;
    }

    public static TimeZoneInfo Poland { get; } = FindPolandZone();

    private static TimeZoneInfo FindPolandZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        }
    }

    public async Task<string?> GetTokenAsync(string userId, CancellationToken ct) =>
        await _db.Users.Where(u => u.Id == userId).Select(u => u.CalendarFeedToken).FirstOrDefaultAsync(ct);

    public async Task<string> RegenerateAsync(string userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstAsync(u => u.Id == userId, ct);
        user.CalendarFeedToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        await _db.SaveChangesAsync(ct);
        return user.CalendarFeedToken;
    }

    public async Task DisableAsync(string userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstAsync(u => u.Id == userId, ct);
        user.CalendarFeedToken = null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string?> BuildIcsAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64) return null;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.CalendarFeedToken == token, ct);
        if (user is null || user.IsDeactivated) return null;

        var permissions = await _permissions.GetPermissionsForRolesAsync(await _userManager.GetRolesAsync(user), ct);
        if (!permissions.Contains(Permissions.MeetingsView)) return null;
        var scope = permissions.Contains(Permissions.DokCasesViewAll) ? CaseScope.All : new CaseScope(false, user.PersonId);

        var from = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime).AddDays(-PastDays);
        var meetings = await _db.Meetings.ForScope(_db, scope).AsNoTracking()
            .Include(m => m.DokCase).ThenInclude(c => c!.Person)
            .Include(m => m.Attendees).ThenInclude(a => a.DokCase).ThenInclude(c => c!.Person)
            .Where(m => m.MeetingDate >= from)
            .ToListAsync(ct);

        var stamp = _time.GetUtcNow().UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");
        var ics = new StringBuilder();
        void Line(string text) => ics.Append(Fold(text)).Append("\r\n");
        Line("BEGIN:VCALENDAR");
        Line("VERSION:2.0");
        Line("PRODID:-//Diecezja Bydgoska//DOK Portal//PL");
        Line("CALSCALE:GREGORIAN");
        Line("METHOD:PUBLISH");
        Line("X-WR-CALNAME:DOK Portal – spotkania");
        Line("X-WR-TIMEZONE:Europe/Warsaw");
        Line("REFRESH-INTERVAL;VALUE=DURATION:PT1H");
        Line("X-PUBLISHED-TTL:PT1H");
        foreach (var m in meetings.OrderBy(m => m.MeetingDate).ThenBy(m => m.StartTime))
        {
            var who = m.DokCase?.Person?.FullName;
            var title = who is not null ? $"Spotkanie: {who}" : $"Zajęcia: {m.GroupLabel ?? "grupa"}";
            var details = new List<string>();
            if (m.Attendees.Count > 0)
                details.Add("Uczestnicy: " + string.Join(", ", m.Attendees.Select(a => a.DokCase?.Person?.FullName).Where(n => n is not null).OrderBy(n => n)));
            if (!string.IsNullOrWhiteSpace(m.Notes)) details.Add(m.Notes!);

            Line("BEGIN:VEVENT");
            Line($"UID:{m.Id}@dokportal");
            Line($"DTSTAMP:{stamp}");
            if (m.StartTime is { } start)
            {
                var end = m.EndTime ?? start.AddHours(1);
                Line($"DTSTART:{Utc(m.MeetingDate, start)}");
                Line($"DTEND:{Utc(m.MeetingDate, end)}");
            }
            else
            {
                Line($"DTSTART;VALUE=DATE:{m.MeetingDate:yyyyMMdd}");
                Line($"DTEND;VALUE=DATE:{m.MeetingDate.AddDays(1):yyyyMMdd}");
            }
            Line($"SUMMARY:{Escape(title)}");
            if (details.Count > 0) Line($"DESCRIPTION:{Escape(string.Join("\n", details))}");
            Line("TRANSP:OPAQUE");
            Line("END:VEVENT");
        }
        Line("END:VCALENDAR");
        return ics.ToString();
    }

    /// <summary>Czas lokalny (Polska) jako UTC w formacie iCal – kalendarz w telefonie sam przeliczy na swoją strefę.</summary>
    public static string Utc(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, Poland).ToString("yyyyMMdd'T'HHmmss'Z'");
    }

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n");

    /// <summary>Linie dłuższe niż 75 bajtów są łamane (wymóg formatu iCal), bez rozcinania znaków UTF-8.</summary>
    private static string Fold(string line)
    {
        if (Encoding.UTF8.GetByteCount(line) <= 75) return line;
        var result = new StringBuilder();
        var current = 0;
        foreach (var ch in line)
        {
            var size = Encoding.UTF8.GetByteCount(ch.ToString());
            if (current + size > 74)
            {
                result.Append("\r\n ");
                current = 1;
            }
            result.Append(ch);
            current += size;
        }
        return result.ToString();
    }
}
