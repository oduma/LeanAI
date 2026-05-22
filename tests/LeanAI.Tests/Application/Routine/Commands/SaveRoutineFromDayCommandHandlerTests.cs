using FluentAssertions;
using LeanAI.Application.Routine.Commands.SaveRoutineFromDay;
using LeanAI.Application.Routine.DTOs;
using LeanAI.Domain.Routine.Entities;
using LeanAI.Domain.Routine.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.Routine.Commands;

public class SaveRoutineFromDayCommandHandlerTests
{
    private readonly Mock<IRoutineRepository>         _repoMock = new();
    private readonly SaveRoutineFromDayCommandHandler _handler;

    public SaveRoutineFromDayCommandHandlerTests()
    {
        _handler = new SaveRoutineFromDayCommandHandler(_repoMock.Object);
        _repoMock.Setup(r => r.ReplaceAllAsync(
                     It.IsAny<IReadOnlyList<RoutineFoodItem>>(),
                     It.IsAny<IReadOnlyList<RoutineActivityItem>>(),
                     It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_EmptyLists_CallsReplaceWithEmptyLists()
    {
        await _handler.Handle(new SaveRoutineFromDayCommand([], []), CancellationToken.None);

        _repoMock.Verify(r => r.ReplaceAllAsync(
            It.Is<IReadOnlyList<RoutineFoodItem>>(l     => l.Count == 0),
            It.Is<IReadOnlyList<RoutineActivityItem>>(l => l.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FoodAndActivityItems_CallsReplaceWithCorrectEntities()
    {
        var foodDto = new RoutineFoodItemDto(Guid.NewGuid(),     "Oatmeal",    "200g", 150);
        var actDto  = new RoutineActivityItemDto(Guid.NewGuid(), "Walk 30min",         120);

        IReadOnlyList<RoutineFoodItem>?     capturedFood     = null;
        IReadOnlyList<RoutineActivityItem>? capturedActivity = null;

        _repoMock.Setup(r => r.ReplaceAllAsync(
                     It.IsAny<IReadOnlyList<RoutineFoodItem>>(),
                     It.IsAny<IReadOnlyList<RoutineActivityItem>>(),
                     It.IsAny<CancellationToken>()))
                 .Callback<IReadOnlyList<RoutineFoodItem>, IReadOnlyList<RoutineActivityItem>, CancellationToken>(
                     (food, act, _) => { capturedFood = food; capturedActivity = act; })
                 .Returns(Task.CompletedTask);

        await _handler.Handle(new SaveRoutineFromDayCommand([foodDto], [actDto]), CancellationToken.None);

        capturedFood.Should().ContainSingle(i =>
            i.Description == "Oatmeal" && i.Quantity == "200g" && i.Calories == 150);
        capturedActivity.Should().ContainSingle(i =>
            i.Description == "Walk 30min" && i.Calories == 120);
    }
}
