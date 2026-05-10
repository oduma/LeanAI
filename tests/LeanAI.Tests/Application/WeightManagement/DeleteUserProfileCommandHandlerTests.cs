using FluentAssertions;
using LeanAI.Application.WeightManagement.Commands.DeleteUserProfile;
using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class DeleteUserProfileCommandHandlerTests
{
    private readonly Mock<IUserProfileRepository> _repositoryMock = new();
    private readonly DeleteUserProfileCommandHandler _handler;

    public DeleteUserProfileCommandHandlerTests()
    {
        _handler = new DeleteUserProfileCommandHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenCalled_CallsDeleteAsyncOnceAndReturnsUnit()
    {
        _repositoryMock
            .Setup(r => r.DeleteAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(new DeleteUserProfileCommand(), CancellationToken.None);

        result.Should().Be(Unit.Value);
        _repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDeleteAsyncThrows_PropagatesException()
    {
        _repositoryMock
            .Setup(r => r.DeleteAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var act = async () => await _handler.Handle(new DeleteUserProfileCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("DB error");
    }
}
