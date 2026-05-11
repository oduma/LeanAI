using LeanAI.Application.WeightManagement.Services;
using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.GetGoogleSpreadsheets;

public sealed record GetGoogleSpreadsheetsQuery(string AccessToken)
    : IRequest<IReadOnlyList<SpreadsheetSummary>>;
