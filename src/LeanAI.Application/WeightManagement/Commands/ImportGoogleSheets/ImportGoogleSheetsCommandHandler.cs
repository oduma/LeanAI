using LeanAI.Application.WeightManagement.Commands.GenerateIdealPath;
using LeanAI.Application.WeightManagement.Commands.UpdateGoalFromImport;
using LeanAI.Application.WeightManagement.DTOs;
using LeanAI.Application.WeightManagement.Services;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Enums;
using LeanAI.Domain.WeightManagement.Interfaces;
using LeanAI.Domain.WeightManagement.Services;
using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.ImportGoogleSheets;

public sealed class ImportGoogleSheetsCommandHandler(
    IGoogleSheetsService         sheetsService,
    IDailyActualWeightRepository actualRepo,
    IUserProfileRepository       profileRepo,
    IMediator                    mediator)
    : IRequestHandler<ImportGoogleSheetsCommand, ImportSummaryDto>
{
    public async Task<ImportSummaryDto> Handle(ImportGoogleSheetsCommand request, CancellationToken cancellationToken)
    {
        var rows = await sheetsService.GetRowsAsync(
            request.SpreadsheetId,
            request.DateColumn,
            request.WeightColumn,
            request.NotesColumn,
            request.AccessToken,
            cancellationToken);

        var failed = rows.Count(r => r.ParseFailed);

        // All rows with a valid date — defines the ideal weight period
        var allDatedRows = rows
            .Where(r => !r.ParseFailed && r.Date.HasValue)
            .OrderBy(r => r.Date!.Value)
            .ToList();

        // Subset that also have a recorded weight — written to DailyActualWeight
        var weightedRows = allDatedRows
            .Where(r => r.WeightKg is > 0)
            .ToList();

        int totalDatesFound       = allDatedRows.Count;
        int datesWithWeightsFound = weightedRows.Count;

        if (allDatedRows.Count == 0)
            return new ImportSummaryDto(0, 0, 0, 0, failed, default, default);

        // Phase 1 — establish the goal period and regenerate the ideal weight line
        // firstDate = first day a weight was recorded (starting point of the ideal line)
        // lastDate  = last date in the sheet, including future dates without weights
        var firstDate    = weightedRows.Count > 0
            ? weightedRows.First().Date!.Value
            : allDatedRows.First().Date!.Value;
        var lastDate     = allDatedRows.Last().Date!.Value;
        var totalDays    = (lastDate.DayNumber - firstDate.DayNumber) + 1;
        var firstWeightKg = weightedRows.Count > 0
            ? ToKg(weightedRows.First().WeightKg!.Value, request.SheetUnitSystem)
            : 0;

        await mediator.Send(new UpdateGoalFromImportCommand(firstDate, lastDate, firstWeightKg), cancellationToken);

        var profile = await profileRepo.GetAsync(cancellationToken);
        if (profile is not null)
        {
            await mediator.Send(new GenerateIdealPathCommand(
                StartDate:        firstDate,
                StartingWeightKg: firstWeightKg,
                TargetWeightKg:   profile.TargetWeightKg ?? firstWeightKg,
                TargetPeriod:     profile.TargetPeriod   ?? TargetPeriod.OneYear,
                ExactTotalDays:   totalDays),
                cancellationToken);
        }

        // Phase 2 — write actual weight rows to DailyActualWeight
        var existingEntries = await actualRepo.GetRangeAsync(firstDate, lastDate, cancellationToken);
        var existingByDate  = existingEntries.ToDictionary(e => e.Date);

        var toInsert       = new List<DailyActualWeight>();
        var weightImported = 0;

        foreach (var row in weightedRows)
        {
            var date     = row.Date!.Value;
            var weightKg = ToKg(row.WeightKg!.Value, request.SheetUnitSystem);
            var notes    = row.Notes;

            if (existingByDate.TryGetValue(date, out var existing))
            {
                existing.WeightKg = weightKg;
                existing.Notes    = notes;
                await actualRepo.UpsertAsync(existing, cancellationToken);
            }
            else
            {
                toInsert.Add(new DailyActualWeight { Date = date, WeightKg = weightKg, Notes = notes });
            }

            weightImported++;
        }

        if (toInsert.Count > 0)
            await actualRepo.InsertBatchAsync(toInsert, cancellationToken);

        return new ImportSummaryDto(
            TotalDatesFound:       totalDatesFound,
            IdealDatesImported:    totalDays,
            DatesWithWeightsFound: datesWithWeightsFound,
            WeightRowsImported:    weightImported,
            Failed:                failed,
            FirstDate:             firstDate,
            LastDate:              lastDate);
    }

    private static double ToKg(double value, UnitSystem unit) =>
        unit == UnitSystem.Imperial ? UnitConverter.LbToKg(value) : value;
}
