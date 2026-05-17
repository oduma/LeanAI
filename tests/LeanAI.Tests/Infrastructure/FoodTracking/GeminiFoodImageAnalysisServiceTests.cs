using FluentAssertions;
using LeanAI.Infrastructure.FoodTracking.Services;

namespace LeanAI.Tests.Infrastructure.FoodTracking;

public class GeminiFoodImageAnalysisServiceTests
{
    [Fact]
    public void ParseResponse_ValidJsonArray_ReturnsMappedDtos()
    {
        const string raw = """
            [
              { "food_item": "Grilled Chicken", "quantity": "150g", "calories": 248.5 },
              { "food_item": "Brown Rice",      "quantity": "200g", "calories": 220   }
            ]
            """;

        var result = GeminiFoodImageAnalysisService.ParseResponse(raw);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(d => d.FoodItem == "Grilled Chicken" && d.Quantity == "150g" && d.Calories == 248.5);
        result.Should().ContainSingle(d => d.FoodItem == "Brown Rice"      && d.Quantity == "200g" && d.Calories == 220.0);
    }

    [Fact]
    public void ParseResponse_WithMarkdownFences_StripsAndSucceeds()
    {
        const string raw = """
            ```json
            [{ "food_item": "Pasta", "quantity": "250g", "calories": 310 }]
            ```
            """;

        var result = GeminiFoodImageAnalysisService.ParseResponse(raw);

        result.Should().HaveCount(1);
        result[0].FoodItem.Should().Be("Pasta");
        result[0].Calories.Should().Be(310.0);
    }

    [Fact]
    public void ParseResponse_EmptyArray_ThrowsInvalidOperationException()
    {
        var act = () => GeminiFoodImageAnalysisService.ParseResponse("[]");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Could not extract food items from the provided image.");
    }

    [Fact]
    public void ParseResponse_ItemMissingFoodItemField_ThrowsInvalidOperationException()
    {
        const string raw = """[{ "quantity": "150g", "calories": 248 }]""";

        var act = () => GeminiFoodImageAnalysisService.ParseResponse(raw);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Could not extract food items from the provided image.");
    }

    [Fact]
    public void ParseResponse_ItemMissingCaloriesField_ThrowsInvalidOperationException()
    {
        const string raw = """[{ "food_item": "Chicken", "quantity": "150g" }]""";

        var act = () => GeminiFoodImageAnalysisService.ParseResponse(raw);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Could not extract food items from the provided image.");
    }

    [Fact]
    public void ParseResponse_MalformedJson_ThrowsInvalidOperationException()
    {
        var act = () => GeminiFoodImageAnalysisService.ParseResponse("not json at all");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Could not extract food items from the provided image.");
    }
}
