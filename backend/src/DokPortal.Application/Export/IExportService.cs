namespace DokPortal.Application.Export;

public interface IExportService
{
    Task<byte[]> ExportPeopleAsync(CancellationToken ct);
}
