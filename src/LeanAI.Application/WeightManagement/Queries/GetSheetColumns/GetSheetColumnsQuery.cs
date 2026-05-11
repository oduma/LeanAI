using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.GetSheetColumns;

public sealed record GetSheetColumnsQuery(string SpreadsheetId, string AccessToken)
    : IRequest<IReadOnlyList<string>>;
