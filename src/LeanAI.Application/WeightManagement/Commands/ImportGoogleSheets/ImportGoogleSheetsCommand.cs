using LeanAI.Application.WeightManagement.DTOs;
using LeanAI.Domain.WeightManagement.Enums;
using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.ImportGoogleSheets;

public sealed record ImportGoogleSheetsCommand(
    string     SpreadsheetId,
    string     DateColumn,
    string     WeightColumn,
    string?    NotesColumn,
    UnitSystem SheetUnitSystem,
    string     AccessToken
) : IRequest<ImportSummaryDto>;
