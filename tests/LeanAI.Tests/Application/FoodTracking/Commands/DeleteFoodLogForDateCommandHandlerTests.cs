using FluentAssertions;
using LeanAI.Application.FoodTracking.Commands.DeleteFoodLog;
using LeanAI.Domain.FoodTracking.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.FoodTracking.Commands;

public class DeleteFoodLogForDateCommandHandlerTests
{
    private readonly Mock<IFoodLogRepository>          _repoMock = new();
    private readonly DeleteFoodLogForDateCommandHandler _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 16);

    public DeleteFoodLogForDateCommandHandlerTests()
    {
        _handler = new DeleteFoodLogForDateCommandHandler(_repoMock.Object);

        _repoMock
            .Setup(r => r.DeleteByDateAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_CallsDeleteByDateWithCorrectDate()
    {
        await _handler.Handle(new DeleteFoodLogForDateCommand(TestDate), CancellationToken.None);

        _repoMock.Verify(
            r => r.DeleteByDateAsync(TestDate, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
