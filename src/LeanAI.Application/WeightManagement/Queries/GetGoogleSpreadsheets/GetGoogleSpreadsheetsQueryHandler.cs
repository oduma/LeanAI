using LeanAI.Application.WeightManagement.Services;
using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.GetGoogleSpreadsheets;

public sealed class GetGoogleSpreadsheetsQueryHandler(IGoogleSheetsService sheetsService)
    : IRequestHandler<GetGoogleSpreadsheetsQuery, IReadOnlyList<SpreadsheetSummary>>
{
    public Task<IReadOnlyList<SpreadsheetSummary>> Handle(
        GetGoogleSpreadsheetsQuery request, CancellationToken cancellationToken) =>
        sheetsService.GetSpreadsheetsAsync(request.AccessToken, cancellationToken);
}
