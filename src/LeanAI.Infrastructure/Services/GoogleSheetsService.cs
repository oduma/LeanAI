using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using LeanAI.Application.WeightManagement.Services;

namespace LeanAI.Infrastructure.Services;

public class GoogleSheetsService : IGoogleSheetsService
{
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy",
        "d/M/yyyy",   "M/d/yyyy",   "yyyy/MM/dd",
        "d-M-yyyy",   "M-d-yyyy",   "dd-MM-yyyy",
        "d MMMM yyyy", "dd MMMM yyyy", "MMMM d yyyy", "MMMM dd yyyy",
        "d MMM yyyy",  "dd MMM yyyy",  "MMM d yyyy",  "MMM dd yyyy"
    ];

    public async Task<IReadOnlyList<SpreadsheetSummary>> GetSpreadsheetsAsync(
        string accessToken, CancellationToken ct = default)
    {
        var credential = GoogleCredential.FromAccessToken(accessToken);
        var service    = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName       = "LeanAI"
        });

        var request = service.Files.List();
        request.Q         = "mimeType='application/vnd.google-apps.spreadsheet' and trashed=false";
        request.Fields    = "files(id, name)";
        request.PageSize  = 100;

        var response = await request.ExecuteAsync(ct);
        return response.Files
            .Select(f => new SpreadsheetSummary(f.Id, f.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetColumnHeadersAsync(
        string spreadsheetId, string accessToken, CancellationToken ct = default)
    {
        var service = BuildSheetsService(accessToken);

        var request = service.Spreadsheets.Values.Get(spreadsheetId, "1:1");
        var response = await request.ExecuteAsync(ct);

        var firstRow = response.Values?.FirstOrDefault();
        if (firstRow is null) return [];

        return firstRow.Select((c, i) =>
        {
            var text = c?.ToString()?.Trim();
            return string.IsNullOrEmpty(text) ? $"Column {(char)('A' + i)}" : text;
        }).ToList();
    }

    public async Task<IReadOnlyList<SheetRow>> GetRowsAsync(
        string spreadsheetId,
        string dateColumn,
        string weightColumn,
        string? notesColumn,
        string accessToken,
        CancellationToken ct = default)
    {
        var service = BuildSheetsService(accessToken);

        // Read header row first to determine column indices
        var headerReq      = service.Spreadsheets.Values.Get(spreadsheetId, "1:1");
        var headerResponse = await headerReq.ExecuteAsync(ct);
        var headers        = headerResponse.Values?.FirstOrDefault()?.Select(c => c?.ToString()?.Trim()).ToList()
                             ?? [];

        int dateIdx   = FindColumnIndex(headers, dateColumn);
        int weightIdx = FindColumnIndex(headers, weightColumn);
        int notesIdx  = notesColumn is not null ? FindColumnIndex(headers, notesColumn) : -1;

        // Read data rows (skip row 1 which is the header); cap at 400 rows
        var dataReq      = service.Spreadsheets.Values.Get(spreadsheetId, "2:401");
        var dataResponse = await dataReq.ExecuteAsync(ct);

        var rows = new List<SheetRow>();
        if (dataResponse.Values is null) return rows;

        foreach (var row in dataResponse.Values)
        {
            var dateCell   = GetCell(row, dateIdx);
            var weightCell = GetCell(row, weightIdx);
            var notesCell  = notesIdx >= 0 ? GetCell(row, notesIdx) : null;

            if (string.IsNullOrWhiteSpace(dateCell) && string.IsNullOrWhiteSpace(weightCell))
                continue; // blank row

            // Unparseable date is always a failure
            if (!TryParseDate(dateCell, out var date))
            {
                rows.Add(new SheetRow(null, null, notesCell, ParseFailed: true));
                continue;
            }

            // Empty weight means the user didn't record that day → Skipped, not Failed
            if (string.IsNullOrWhiteSpace(weightCell))
            {
                rows.Add(new SheetRow(date, null, notesCell?.Trim(), ParseFailed: false));
                continue;
            }

            // Non-empty but unparseable weight is a failure
            if (!TryParseWeight(weightCell, out var weight))
            {
                rows.Add(new SheetRow(null, null, notesCell, ParseFailed: true));
                continue;
            }

            rows.Add(new SheetRow(date, weight, notesCell?.Trim(), ParseFailed: false));
        }

        return rows;
    }

    private static SheetsService BuildSheetsService(string accessToken)
    {
        var credential = GoogleCredential.FromAccessToken(accessToken);
        return new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName       = "LeanAI"
        });
    }

    private static int FindColumnIndex(List<string?> headers, string columnName)
    {
        var idx = headers.FindIndex(h => string.Equals(h, columnName, StringComparison.OrdinalIgnoreCase));
        return idx >= 0 ? idx : 0;
    }

    private static string? GetCell(IList<object> row, int index) =>
        index >= 0 && index < row.Count ? row[index]?.ToString() : null;

    private static bool TryParseDate(string? value, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var trimmed = value.Trim();

        foreach (var fmt in DateFormats)
        {
            if (DateOnly.TryParseExact(trimmed, fmt,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out date))
                return true;
        }

        // Broad fallback: handles locale-specific formats Google Sheets may return
        if (DateTime.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var dt))
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        if (DateTime.TryParse(trimmed, System.Globalization.CultureInfo.CurrentCulture,
                System.Globalization.DateTimeStyles.AllowWhiteSpaces, out dt))
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        return false;
    }

    private static bool TryParseWeight(string? value, out double weight)
    {
        weight = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var normalised = value.Trim().Replace(',', '.');
        return double.TryParse(normalised,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out weight);
    }
}
