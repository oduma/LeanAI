using FluentAssertions;
using LeanAI.Application.WeightManagement.Queries.GetGoogleSpreadsheets;
using LeanAI.Application.WeightManagement.Queries.GetSheetColumns;
using LeanAI.Application.WeightManagement.Services;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class GoogleSpreadsheetsQueryHandlerTests
{
    private readonly Mock<IGoogleSheetsService> _serviceMock = new();

    [Fact]
    public async Task GetSpreadsheets_ReturnsMappedList()
    {
        var expected = new List<SpreadsheetSummary>
        {
            new("id1", "Sheet A"),
            new("id2", "Sheet B")
        };
        _serviceMock
            .Setup(s => s.GetSpreadsheetsAsync("token123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetGoogleSpreadsheetsQueryHandler(_serviceMock.Object);
        var result  = await handler.Handle(new GetGoogleSpreadsheetsQuery("token123"), CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetSheetColumns_ReturnsMappedHeaders()
    {
        var expected = new List<string> { "Date", "Weight", "Notes" };
        _serviceMock
            .Setup(s => s.GetColumnHeadersAsync("sheetId", "token123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetSheetColumnsQueryHandler(_serviceMock.Object);
        var result  = await handler.Handle(new GetSheetColumnsQuery("sheetId", "token123"), CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }
}
