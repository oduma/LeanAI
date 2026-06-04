using FluentAssertions;
using LeanAI.Application.Shared;
using LeanAI.Infrastructure.Shared.Services;

namespace LeanAI.Tests.Infrastructure.Shared;

public class GeminiShareImageClassificationServiceTests
{
    [Theory]
    [InlineData("food",    ShareImageType.Food)]
    [InlineData("Food",    ShareImageType.Food)]
    [InlineData("FOOD",    ShareImageType.Food)]
    [InlineData("  food ", ShareImageType.Food)]
    public void ParseResponse_FoodVariants_ReturnFood(string raw, ShareImageType expected)
        => GeminiShareImageClassificationService.ParseResponse(raw).Should().Be(expected);

    [Theory]
    [InlineData("run",    ShareImageType.Run)]
    [InlineData("Run",    ShareImageType.Run)]
    [InlineData("RUN",    ShareImageType.Run)]
    [InlineData("  run ", ShareImageType.Run)]
    public void ParseResponse_RunVariants_ReturnRun(string raw, ShareImageType expected)
        => GeminiShareImageClassificationService.ParseResponse(raw).Should().Be(expected);

    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData("beach")]
    [InlineData("food and run")]
    [InlineData("Sorry, I cannot classify this.")]
    public void ParseResponse_UnknownVariants_ReturnUnknown(string raw)
        => GeminiShareImageClassificationService.ParseResponse(raw).Should().Be(ShareImageType.Unknown);
}
