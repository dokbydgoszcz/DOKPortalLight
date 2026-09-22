using DokPortal.Application.Dashboard;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db) => _db = db;

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct) => new()
    {
        PeopleCount = await _db.People.CountAsync(ct),
        ParishCount = await _db.Parishes.CountAsync(ct)
    };
}
