using FluentAssertions;
using LeanAI.Application.FoodTracking.Commands.SaveRoutineFromDay;
using LeanAI.Application.FoodTracking.DTOs;
using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.FoodTracking.Commands;

public class SaveRoutineFromDayCommandHandlerTests
{
    private readonly Mock<IRoutineRepository> _repoMock = new();
    private readonly SaveRoutineFromDayCommandHandler _handler;

    public SaveRoutineFromDayCommandHandlerTests()
    {
        _handler = new SaveRoutineFromDayCommandHandler(_repoMock.Object);
        _repoMock.Setup(r => r.ReplaceAllAsync(It.IsAny<IReadOnlyList<RoutineItem>>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_EmptyItems_CallsReplaceWithEmptyList()
    {
        await _handler.Handle(new SaveRoutineFromDayCommand([]), CancellationToken.None);

        _repoMock.Verify(r => r.ReplaceAllAsync(
            It.Is<IReadOnlyList<RoutineItem>>(l => l.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FoodAndActivityItems_CallsReplaceWithCorrectEntities()
    {
        var foodDto = new RoutineItemDto(Guid.NewGuid(), "food", "Oatmeal", "200g", 150);
        var actDto  = new RoutineItemDto(Guid.NewGuid(), "activity", "Walk 30min", null, 120);

        IReadOnlyList<RoutineItem>? captured = null;
        _repoMock.Setup(r => r.ReplaceAllAsync(It.IsAny<IReadOnlyList<RoutineItem>>(), It.IsAny<CancellationToken>()))
                 .Callback<IReadOnlyList<RoutineItem>, CancellationToken>((items, _) => captured = items)
                 .Returns(Task.CompletedTask);

        await _handler.Handle(new SaveRoutineFromDayCommand([foodDto, actDto]), CancellationToken.None);

        captured.Should().HaveCount(2);
        captured.Should().ContainSingle(i => i.SourceType == "food" && i.Description == "Oatmeal" && i.Quantity == "200g" && i.Calories == 150);
        captured.Should().ContainSingle(i => i.SourceType == "activity" && i.Description == "Walk 30min" && i.Quantity == null && i.Calories == 120);
    }
}
