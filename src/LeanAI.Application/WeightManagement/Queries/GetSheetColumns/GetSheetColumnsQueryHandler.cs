using LeanAI.Application.WeightManagement.Services;
using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.GetSheetColumns;

public sealed class GetSheetColumnsQueryHandler(IGoogleSheetsService sheetsService)
    : IRequestHandler<GetSheetColumnsQuery, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> Handle(
        GetSheetColumnsQuery request, CancellationToken cancellationToken) =>
        sheetsService.GetColumnHeadersAsync(request.SpreadsheetId, request.AccessToken, cancellationToken);
}
