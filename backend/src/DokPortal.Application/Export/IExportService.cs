namespace DokPortal.Application.Export;

public interface IExportService
{
    Task<byte[]> ExportPeopleAsync(CancellationToken ct);
    Task<byte[]> ExportDokCasesAsync(CancellationToken ct);
    Task<byte[]> ExportCandidatesAsync(CancellationToken ct);
    Task<byte[]> ExportMissionsAsync(CancellationToken ct);
    Task<byte[]> ExportFormatorsAsync(CancellationToken ct);
    Task<byte[]> ExportSupervisionsAsync(CancellationToken ct);
    Task<byte[]> ExportMeetingsAsync(CancellationToken ct);
    Task<byte[]> ExportParishesAsync(CancellationToken ct);
}
