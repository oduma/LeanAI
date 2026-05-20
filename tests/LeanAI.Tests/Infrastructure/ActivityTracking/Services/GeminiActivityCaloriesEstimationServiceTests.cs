using FluentAssertions;
using LeanAI.Infrastructure.ActivityTracking.Services;

namespace LeanAI.Tests.Infrastructure.ActivityTracking.Services;

public class GeminiActivityCaloriesEstimationServiceTests
{
    [Fact]
    public void ParseResponse_ValidJsonArray_ReturnsMappedCalories()
    {
        var result = GeminiActivityCaloriesEstimationService.ParseResponse("[300, 250]", 2);

        result.Should().HaveCount(2);
        result[0].Should().Be(300.0);
        result[1].Should().Be(250.0);
    }

    [Fact]
    public void ParseResponse_JsonWithMarkdownFences_StripsAndSucceeds()
    {
        var fenced = "```json\n[300, 250]\n```";

        var result = GeminiActivityCaloriesEstimationService.ParseResponse(fenced, 2);

        result.Should().HaveCount(2);
        result[0].Should().Be(300.0);
        result[1].Should().Be(250.0);
    }

    [Fact]
    public void ParseResponse_EmptyArray_ReturnsPaddedZeros()
    {
        var result = GeminiActivityCaloriesEstimationService.ParseResponse("[]", 3);

        result.Should().HaveCount(3);
        result.Should().AllSatisfy(v => v.Should().Be(0.0));
    }

    [Fact]
    public void ParseResponse_MalformedJson_ReturnsPaddedZeros()
    {
        var result = GeminiActivityCaloriesEstimationService.ParseResponse("not json", 2);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(v => v.Should().Be(0.0));
    }

    [Fact]
    public void ParseResponse_FewerElementsThanExpected_PadsWithZero()
    {
        var result = GeminiActivityCaloriesEstimationService.ParseResponse("[300]", 3);

        result.Should().HaveCount(3);
        result[0].Should().Be(300.0);
        result[1].Should().Be(0.0);
        result[2].Should().Be(0.0);
    }

    [Fact]
    public void ParseResponse_MoreElementsThanExpected_TrimsTail()
    {
        var result = GeminiActivityCaloriesEstimationService.ParseResponse("[300, 250, 400]", 2);

        result.Should().HaveCount(2);
        result[0].Should().Be(300.0);
        result[1].Should().Be(250.0);
    }
}
