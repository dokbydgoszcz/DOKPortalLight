using ClosedXML.Excel;
using DokPortal.Application.Budget;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class BudgetService : IBudgetService
{
    private readonly AppDbContext _db;

    public BudgetService(AppDbContext db) => _db = db;

    private IQueryable<BudgetEntry> InPeriod(BudgetFund fund, DateOnly? from, DateOnly? to)
    {
        var q = _db.BudgetEntries.AsNoTracking().Where(e => e.Fund == fund);
        if (from is { } f) q = q.Where(e => e.EntryDate >= f);
        if (to is { } t) q = q.Where(e => e.EntryDate <= t);
        return q;
    }

    public async Task<IReadOnlyList<BudgetEntryDto>> GetEntriesAsync(BudgetFund fund, CancellationToken ct, DateOnly? from = null, DateOnly? to = null)
    {
        var entries = await InPeriod(fund, from, to)
            .OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.CreatedAtUtc)
            .ToListAsync(ct);
        var attachments = await AttachmentLookup.ForOwnersAsync(_db, AttachmentOwnerType.BudgetEntry, entries.Select(e => e.Id).ToList(), ct);
        return entries.Select(e => ToDto(e, attachments.GetValueOrDefault(e.Id))).ToList();
    }

    /// <summary>Sumy liczymy po stronie aplikacji: SQLite (testy) nie sumuje typu decimal.</summary>
    private static (decimal Income, decimal Expense) Totals(IEnumerable<(BudgetEntryType Type, decimal Amount)> rows)
    {
        decimal income = 0, expense = 0;
        foreach (var (type, amount) in rows)
        {
            if (type == BudgetEntryType.Income) income += amount; else expense += amount;
        }
        return (income, expense);
    }

    public async Task<BudgetSummaryDto> GetSummaryAsync(BudgetFund fund, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var rows = await _db.BudgetEntries.AsNoTracking()
            .Where(e => e.Fund == fund && (to == null || e.EntryDate <= to))
            .Select(e => new { e.EntryDate, e.Type, e.Amount })
            .ToListAsync(ct);
        var before = Totals(rows.Where(r => from is not null && r.EntryDate < from).Select(r => (r.Type, r.Amount)));
        var during = Totals(rows.Where(r => from is null || r.EntryDate >= from).Select(r => (r.Type, r.Amount)));
        var opening = before.Income - before.Expense;
        return new BudgetSummaryDto
        {
            OpeningBalance = opening,
            Income = during.Income,
            Expense = during.Expense,
            ClosingBalance = opening + during.Income - during.Expense
        };
    }

    public async Task<byte[]> ExportAsync(BudgetFund fund, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var entries = await InPeriod(fund, from, to).OrderBy(e => e.EntryDate).ThenBy(e => e.CreatedAtUtc).ToListAsync(ct);
        var summary = await GetSummaryAsync(fund, from, to, ct);
        var withFiles = (await AttachmentLookup.ForOwnersAsync(_db, AttachmentOwnerType.BudgetEntry, entries.Select(e => e.Id).ToList(), ct))
            .ToDictionary(kv => kv.Key, kv => string.Join(", ", kv.Value.Select(a => a.FileName)));

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(fund == BudgetFund.DOK ? "Budżet DOK" : "Budżet SKŚP");
        string[] headers = { "Data", "Opis", "Kategoria", "Typ", "Kwota (zł)", "Załączniki" };
        for (var c = 0; c < headers.Length; c++) sheet.Cell(1, c + 1).Value = headers[c];
        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var e in entries)
        {
            sheet.Cell(row, 1).Value = e.EntryDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 1).Style.DateFormat.Format = "yyyy-mm-dd";
            sheet.Cell(row, 2).Value = e.Description;
            sheet.Cell(row, 3).Value = e.Category;
            sheet.Cell(row, 4).Value = e.Type == BudgetEntryType.Income ? "Przychód" : "Wydatek";
            sheet.Cell(row, 5).Value = e.Amount;
            sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            if (withFiles.TryGetValue(e.Id, out var files)) sheet.Cell(row, 6).Value = files;
            row++;
        }

        row++;
        var period = from is null && to is null ? "cały okres" : $"{from:yyyy-MM-dd} – {to:yyyy-MM-dd}".Trim(' ', '–');
        void SummaryRow(string label, decimal value)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 5).Value = value;
            sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }
        sheet.Cell(row++, 1).Value = $"Okres: {period}";
        SummaryRow("Saldo otwarcia", summary.OpeningBalance);
        SummaryRow("Przychody", summary.Income);
        SummaryRow("Wydatki", summary.Expense);
        SummaryRow("Saldo na koniec", summary.ClosingBalance);

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<BudgetEntryDto> CreateAsync(CreateBudgetEntryRequest request, CancellationToken ct)
    {
        var entry = new BudgetEntry
        {
            Id = Guid.NewGuid(),
            Fund = request.Fund,
            EntryDate = request.EntryDate,
            Description = request.Description.Trim(),
            Category = request.Category.Trim(),
            Type = request.Type,
            Amount = request.Amount,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.BudgetEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        return ToDto(entry, null);
    }

    public async Task<BudgetEntryDto?> UpdateAsync(Guid id, UpdateBudgetEntryRequest request, CancellationToken ct)
    {
        var entry = await _db.BudgetEntries.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null) return null;

        entry.EntryDate = request.EntryDate;
        entry.Description = request.Description.Trim();
        entry.Category = request.Category.Trim();
        entry.Type = request.Type;
        entry.Amount = request.Amount;
        entry.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        var attachments = await AttachmentLookup.ForOwnersAsync(_db, AttachmentOwnerType.BudgetEntry, new[] { id }, ct);
        return ToDto(entry, attachments.GetValueOrDefault(id));
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var entry = await _db.BudgetEntries.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null) return false;

        entry.DeletedAtUtc = DateTime.UtcNow;
        entry.DeletedBy = deletedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<BudgetFund?> GetFundAsync(Guid id, CancellationToken ct)
    {
        var entry = await _db.BudgetEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        return entry?.Fund;
    }

    private static BudgetEntryDto ToDto(BudgetEntry e, IReadOnlyList<DokPortal.Application.Attachments.AttachmentDto>? attachments) => new()
    {
        Id = e.Id,
        Fund = e.Fund,
        EntryDate = e.EntryDate,
        Description = e.Description,
        Category = e.Category,
        Type = e.Type,
        Amount = e.Amount,
        Attachments = attachments ?? (IReadOnlyList<DokPortal.Application.Attachments.AttachmentDto>)Array.Empty<DokPortal.Application.Attachments.AttachmentDto>()
    };
}
