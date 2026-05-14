using FluentAssertions;
using LeanAI.Application.WeightManagement.Commands.GenerateIdealPath;
using LeanAI.Application.WeightManagement.Commands.ImportGoogleSheets;
using LeanAI.Application.WeightManagement.Commands.UpdateGoalFromImport;
using LeanAI.Application.WeightManagement.Services;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Enums;
using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class ImportGoogleSheetsCommandHandlerTests
{
    private readonly Mock<IGoogleSheetsService>         _serviceMock     = new();
    private readonly Mock<IDailyActualWeightRepository> _actualRepoMock  = new();
    private readonly Mock<IUserProfileRepository>       _profileRepoMock = new();
    private readonly Mock<IMediator>                    _mediatorMock    = new();
    private readonly Mock<IWeeklyAverageRepository>     _weeklyRepoMock  = new();
    private readonly ImportGoogleSheetsCommandHandler   _handler;

    private static readonly DateOnly Day1 = new(2025, 1, 10);
    private static readonly DateOnly Day2 = new(2025, 1, 11);
    private static readonly DateOnly Day3 = new(2025, 1, 12);
    private static readonly DateOnly FutureDay = new(2025, 12, 31);

    private static readonly ImportGoogleSheetsCommand BaseCmd = new(
        SpreadsheetId:   "sheetId",
        DateColumn:      "Date",
        WeightColumn:    "Weight",
        NotesColumn:     "Notes",
        SheetUnitSystem: UnitSystem.Metric,
        AccessToken:     "token"
    );

    public ImportGoogleSheetsCommandHandlerTests()
    {
        _handler = new ImportGoogleSheetsCommandHandler(
            _serviceMock.Object,
            _actualRepoMock.Object,
            _profileRepoMock.Object,
            _mediatorMock.Object,
            _weeklyRepoMock.Object);

        _weeklyRepoMock
            .Setup(r => r.UpsertAsync(It.IsAny<WeeklyAverage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _profileRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile
            {
                TargetWeightKg   = 70.0,
                TargetPeriod     = TargetPeriod.OneYear,
                StartingWeightKg = 90.0
            });

        _actualRepoMock
            .Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DailyActualWeight>());

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpdateGoalFromImportCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Unit.Value));
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GenerateIdealPathCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Unit.Value));
    }

    [Fact]
    public async Task Handle_InsertsNewEntriesForDatesWithNoExistingData()
    {
        _serviceMock
            .Setup(s => s.GetRowsAsync("sheetId", "Date", "Weight", "Notes", "token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SheetRow>
            {
                new(Day1, 88.0, "good day", false),
                new(Day2, 87.5, null,       false)
            });

        IEnumerable<DailyActualWeight>? inserted = null;
        _actualRepoMock
            .Setup(r => r.InsertBatchAsync(It.IsAny<IEnumerable<DailyActualWeight>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<DailyActualWeight>, CancellationToken>((e, _) => inserted = e)
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(BaseCmd, CancellationToken.None);

        result.WeightRowsImported.Should().Be(2);
        result.DatesWithWeightsFound.Should().Be(2);
        result.TotalDatesFound.Should().Be(2);
        result.Failed.Should().Be(0);
        inserted.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_DateWithNoWeight_CountedInTotalButNotImportedAsWeight()
    {
        _serviceMock
            .Setup(s => s.GetRowsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SheetRow>
            {
                new(Day1, null, "no weight", false),
                new(Day2, 87.0, null,        false)
            });

        var result = await _handler.Handle(BaseCmd, CancellationToken.None);

        result.TotalDatesFound.Should().Be(2);
        result.DatesWithWeightsFound.Should().Be(1);
        result.WeightRowsImported.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DateWithZeroWeight_CountedInTotalButNotImportedAsWeight()
    {
        _serviceMock
            .Setup(s => s.GetRowsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SheetRow>
            {
                new(Day1, 0.0,  "zero weight", false),
                new(Day2, 88.0, null,          false)
            });

        var result = await _handler.Handle(BaseCmd, CancellationToken.None);

        result.TotalDatesFound.Should().Be(2);
        result.DatesWithWeightsFound.Should().Be(1);
        result.WeightRowsImported.Should().Be(1);
    }

    [Fact]
    public async Task Handle_CountsFailedRows()
    {
        _serviceMock
            .Setup(s => s.GetRowsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SheetRow>
            {
                new(null, null, null, true),   // parse failed
                new(Day1, 88.0, null, false)
            });

        var result = await _handler.Handle(BaseCmd, CancellationToken.None);

        result.Failed.Should().Be(1);
        result.WeightRowsImported.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UpdatesExistingEntry_WhenImportedWeightIsValid()
    {
        var existing = new DailyActualWeight { Date = Day1, WeightKg = 90.0, Notes = "old" };
        _actualRepoMock
            .Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DailyActualWeight> { existing });

        _serviceMock
            .Setup(s => s.GetRowsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SheetRow>
            {
                new(Day1, 88.5, "new note", false)
            });

        DailyActualWeight? upserted = null;
        _actualRepoMock
            .Setup(r => r.UpsertAsync(It.IsAny<DailyActualWeight>(), It.IsAny<CancellationToken>()))
            .Callback<DailyActualWeight, CancellationToken>((e, _) => upserted = e)
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(BaseCmd, CancellationToken.None);

        result.WeightRowsImported.Should().Be(1);
        upserted.Should().BeSameAs(existing);
        existing.WeightKg.Should().Be(88.5);
        existing.Notes.Should().Be("new note");
    }

    [Fact]
    public async Task Handle_DoesNotUpsert_WhenImportedWeightIsZero()
    {
        var existing = new DailyActualWeight { Date = Day1, WeightKg = 90.0 };
        _actualRepoMock
            .Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DailyActualWeight> { existing });

        _serviceMock
            .Setup(s => s.GetRowsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SheetRow>
            {
                new(Day1, 0.0, null, false)   // zero weight → not imported as weight entry
            });

        var result = await _handler.Handle(BaseCmd, CancellationToken.None);

        result.WeightRowsImported.Should().Be(0);
        _actualRepoMock.Verify(r => r.UpsertAsync(It.IsAny<DailyActualWeight>(), It.IsAny<CancellationToken>()), Times.Never);
        existing.WeightKg.Should().Be(90.0);  // unchanged
    }

    [Fact]
    public async Task Handle_ConvertsImperialWeightToKg()
    {
        var cmd = BaseCmd with { SheetUnitSystem = UnitSystem.Imperial };

        _serviceMock
            .Setup(s => s.GetRowsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SheetRow>
            {
                new(Day1, 198.0, null, false)   // 198 lb
            });

        IEnumerable<DailyActualWeight>? inserted = null;
        _actualRepoMock
            .Setup(r => r.InsertBatchAsync(It.IsAny<IEnumerable<DailyActualWeight>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<DailyActualWeight>, CancellationToken>((e, _) => inserted = e)
            .Returns(Task.CompletedTask);

        await _handler.Handle(cmd, CancellationToken.None);

        // 198 lb × 0.45359237 ≈ 89.81 kg
        inserted!.Single().WeightKg.Should().BeApproximately(198.0 * 0.45359237, 0.001);
    }

    [Fact]
    public async Task Handle_UsesLastDatedRow_ForGoalEnd_EvenWhenNoWeightRecorded()
    {
        // FutureDay has no weight yet — it's in the spreadsheet for the goal end date
        _serviceMock
            .Setup(s => s.GetRowsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SheetRow>
            {
                new(Day1,      88.0, null, false),
                new(Day2,      87.5, null, false),
                new(FutureDay, null, null, false)   // future date, no weight yet
            });

        var result = await _handler.Handle(BaseCmd, CancellationToken.None);

        result.FirstDate.Should().Be(Day1);
        result.LastDate.Should().Be(FutureDay);
        result.TotalDatesFound.Should().Be(3);
        result.DatesWithWeightsFound.Should().Be(2);
        result.WeightRowsImported.Should().Be(2);
    }

    [Fact]
    public async Task Handle_TriggersUpdateGoalFromImport_WithFirstWeightedDateAndLastSheetDate()
    {
        _serviceMock
            .Setup(s => s.GetRowsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SheetRow>
            {
                new(Day1,      88.0, null, false),
                new(Day2,      87.5, null, false),
                new(FutureDay, null, null, false)
            });

        await _handler.Handle(BaseCmd, CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<UpdateGoalFromImportCommand>(c =>
                c.GoalStartDate    == Day1      &&
                c.GoalEndDate      == FutureDay &&
                c.StartingWeightKg == 88.0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TriggersIdealPathRegeneration_WithExactDayCount()
    {
        _serviceMock
            .Setup(s => s.GetRowsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SheetRow>
            {
                new(Day1, 88.0, null, false),
                new(Day3, 87.0, null, false)   // Day3 - Day1 = 2 days apart → total 3 days
            });

        await _handler.Handle(BaseCmd, CancellationToken.None);

        // ExactTotalDays = (Day3 - Day1).Days + 1 = 2 + 1 = 3
        _mediatorMock.Verify(m => m.Send(
            It.Is<GenerateIdealPathCommand>(c =>
                c.StartDate        == Day1   &&
                c.StartingWeightKg == 88.0   &&
                c.TargetWeightKg   == 70.0   &&
                c.ExactTotalDays   == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AfterImport_UpsertsWeeklyAveragePerWeek()
    {
        // Day1=Jan 10 (Fri), Day2=Jan 11 (Sat), Day3=Jan 12 (Sun) — all in week Jan 6–12
        _serviceMock
            .Setup(s => s.GetRowsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SheetRow>
            {
                new(Day1, 88.0, null, false),
                new(Day2, 87.0, null, false),
                new(Day3, 86.0, null, false),
            });

        _actualRepoMock
            .Setup(r => r.InsertBatchAsync(It.IsAny<IEnumerable<DailyActualWeight>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var upsertedAverages = new List<WeeklyAverage>();
        _weeklyRepoMock
            .Setup(r => r.UpsertAsync(It.IsAny<WeeklyAverage>(), It.IsAny<CancellationToken>()))
            .Callback<WeeklyAverage, CancellationToken>((e, _) => upsertedAverages.Add(e))
            .Returns(Task.CompletedTask);

        await _handler.Handle(BaseCmd, CancellationToken.None);

        upsertedAverages.Should().HaveCount(1);
        upsertedAverages[0].WeekStart.Should().Be(new DateOnly(2025, 1, 6)); // Monday of Jan 10–12 week
        upsertedAverages[0].AverageWeightKg.Should().BeApproximately((88.0 + 87.0 + 86.0) / 3, 0.001);
    }
}
