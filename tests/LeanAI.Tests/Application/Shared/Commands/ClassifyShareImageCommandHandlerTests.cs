using FluentAssertions;
using LeanAI.Application.Shared;
using LeanAI.Application.Shared.Commands.ClassifyShareImage;
using LeanAI.Application.Shared.Services;
using Moq;

namespace LeanAI.Tests.Application.Shared.Commands;

public class ClassifyShareImageCommandHandlerTests
{
    private readonly Mock<IShareImageClassificationService> _serviceMock = new();
    private readonly ClassifyShareImageCommandHandler       _handler;

    private static readonly byte[] DummyBytes = [1, 2, 3];
    private const string           JpegMime   = "image/jpeg";

    public ClassifyShareImageCommandHandlerTests()
    {
        _handler = new ClassifyShareImageCommandHandler(_serviceMock.Object);
    }

    [Fact]
    public async Task Handle_FoodImage_ReturnsFoodType()
    {
        _serviceMock
            .Setup(s => s.ClassifyAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShareImageType.Food);

        var result = await _handler.Handle(new ClassifyShareImageCommand(DummyBytes, JpegMime), CancellationToken.None);

        result.Should().Be(ShareImageType.Food);
    }

    [Fact]
    public async Task Handle_RunImage_ReturnsRunType()
    {
        _serviceMock
            .Setup(s => s.ClassifyAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShareImageType.Run);

        var result = await _handler.Handle(new ClassifyShareImageCommand(DummyBytes, JpegMime), CancellationToken.None);

        result.Should().Be(ShareImageType.Run);
    }

    [Fact]
    public async Task Handle_UnknownImage_ReturnsUnknownType()
    {
        _serviceMock
            .Setup(s => s.ClassifyAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShareImageType.Unknown);

        var result = await _handler.Handle(new ClassifyShareImageCommand(DummyBytes, JpegMime), CancellationToken.None);

        result.Should().Be(ShareImageType.Unknown);
    }
}
