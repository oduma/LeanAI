namespace LeanAI.Application.WeightManagement.Services;

public interface IGoogleSheetsService
{
    Task<IReadOnlyList<SpreadsheetSummary>> GetSpreadsheetsAsync(string accessToken, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetColumnHeadersAsync(string spreadsheetId, string accessToken, CancellationToken ct = default);
    Task<IReadOnlyList<SheetRow>> GetRowsAsync(string spreadsheetId, string dateColumn, string weightColumn, string? notesColumn, string accessToken, CancellationToken ct = default);
}

public sealed record SpreadsheetSummary(string Id, string Name);

public sealed record SheetRow(DateOnly? Date, double? WeightKg, string? Notes, bool ParseFailed);
