using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DokPortal.Infrastructure.Auth;

/// <summary>
/// Sprawdza przy każdym żądaniu, czy token nadal odpowiada kontu: konto istnieje, nie jest wyłączone, a znacznik bezpieczeństwa
/// (zmieniany przy zmianie ról, haseł i wyłączeniu konta) jest ten sam co w tokenie. Wynik jest pamiętany 60 s, żeby nie obciążać
/// darmowej bazy; zmiany konta w tej aplikacji czyszczą pamięć od razu (<see cref="Invalidate"/>).
/// </summary>
public class SessionValidator
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public SessionValidator(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    private static string Key(string userId) => $"session:{userId}";

    public async Task<bool> IsValidAsync(string? userId, string? stamp, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(stamp)) return false;

        if (!_cache.TryGetValue(Key(userId), out (string? Stamp, bool Deactivated)? account) || account is null)
        {
            var row = await _db.Users.AsNoTracking().Where(u => u.Id == userId)
                .Select(u => new { u.SecurityStamp, u.IsDeactivated }).FirstOrDefaultAsync(ct);
            account = row is null ? (null, true) : (row.SecurityStamp, row.IsDeactivated);
            _cache.Set(Key(userId), account, CacheTtl);
        }

        return !account.Value.Deactivated && account.Value.Stamp == stamp;
    }

    public void Invalidate(string userId) => _cache.Remove(Key(userId));
}
