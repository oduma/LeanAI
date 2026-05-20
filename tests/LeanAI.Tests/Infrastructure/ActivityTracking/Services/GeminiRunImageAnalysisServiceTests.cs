using FluentAssertions;
using LeanAI.Infrastructure.ActivityTracking.Services;

namespace LeanAI.Tests.Infrastructure.ActivityTracking.Services;

public class GeminiRunImageAnalysisServiceTests
{
    private static string ValidJson =>
        """
        [
          {"parameter_name":"distance","value":"5.2","unit":"km"},
          {"parameter_name":"pace","value":"5:30","unit":"min/km"},
          {"parameter_name":"duration","value":"28:36","unit":"min"}
        ]
        """;

    [Fact]
    public void ParseResponse_ValidJsonArray_ReturnsMappedMetrics()
    {
        var result = GeminiRunImageAnalysisService.ParseResponse(ValidJson);

        result.Should().HaveCount(3);
        result.Should().ContainSingle(m => m.ParameterName == "distance" && m.Value == "5.2"   && m.Unit == "km");
        result.Should().ContainSingle(m => m.ParameterName == "pace"     && m.Value == "5:30"  && m.Unit == "min/km");
        result.Should().ContainSingle(m => m.ParameterName == "duration" && m.Value == "28:36" && m.Unit == "min");
    }

    [Fact]
    public void ParseResponse_JsonWithMarkdownFences_StripsAndSucceeds()
    {
        var fenced = $"```json\n{ValidJson}\n```";

        var result = GeminiRunImageAnalysisService.ParseResponse(fenced);

        result.Should().HaveCount(3);
        result.Should().ContainSingle(m => m.ParameterName == "distance");
        result.Should().ContainSingle(m => m.ParameterName == "pace");
        result.Should().ContainSingle(m => m.ParameterName == "duration");
    }

    [Fact]
    public void ParseResponse_EmptyArray_ThrowsInvalidOperationException()
    {
        var act = () => GeminiRunImageAnalysisService.ParseResponse("[]");

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Could not extract run metrics from the provided image.");
    }

    [Fact]
    public void ParseResponse_MissingRequiredParameter_ThrowsInvalidOperationException()
    {
        var incomplete =
            """
            [
              {"parameter_name":"distance","value":"5.2","unit":"km"},
              {"parameter_name":"pace","value":"5:30","unit":"min/km"}
            ]
            """;

        var act = () => GeminiRunImageAnalysisService.ParseResponse(incomplete);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Could not extract run metrics from the provided image.");
    }

    [Fact]
    public void ParseResponse_MalformedJson_ThrowsInvalidOperationException()
    {
        var act = () => GeminiRunImageAnalysisService.ParseResponse("not json at all");

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Could not extract run metrics from the provided image.");
    }

    [Fact]
    public void ParseResponse_WithCaloriesBurned_IncludesCaloriesInResult()
    {
        var json =
            """
            [
              {"parameter_name":"distance","value":"5.2","unit":"km"},
              {"parameter_name":"pace","value":"5:30","unit":"min/km"},
              {"parameter_name":"duration","value":"28:36","unit":"min"},
              {"parameter_name":"calories_burned","value":"320","unit":"kcal"}
            ]
            """;

        var result = GeminiRunImageAnalysisService.ParseResponse(json);

        result.Should().HaveCount(4);
        result.Should().ContainSingle(m =>
            m.ParameterName == "calories_burned" && m.Value == "320" && m.Unit == "kcal");
    }

    [Fact]
    public void ParseResponse_WithoutCaloriesBurned_StillSucceeds()
    {
        var result = GeminiRunImageAnalysisService.ParseResponse(ValidJson);

        result.Should().HaveCount(3);
        result.Should().NotContain(m => m.ParameterName == "calories_burned");
    }
}
