namespace DokPortal.Application.Export;

public interface IExportService
{
    Task<byte[]> ExportPeopleAsync(CancellationToken ct);
    Task<byte[]> ExportDokCasesAsync(CancellationToken ct);
    Task<byte[]> ExportCandidatesAsync(CancellationToken ct);
}
