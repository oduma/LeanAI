using FluentAssertions;
using LeanAI.Application.Common;
using LeanAI.Application.WeightManagement.Queries.GetAppSettings;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class GetAppSettingsQueryHandlerTests
{
    private readonly Mock<IAppSettingsRepository> _settingsRepoMock = new();
    private readonly Mock<IApiKeyStorage> _apiKeyStorageMock = new();
    private readonly GetAppSettingsQueryHandler _handler;

    public GetAppSettingsQueryHandlerTests()
    {
        _handler = new GetAppSettingsQueryHandler(_settingsRepoMock.Object, _apiKeyStorageMock.Object);
    }

    [Fact]
    public async Task Handle_WhenNoSettings_AndNoKey_ReturnsDefaultModelNameAndEmptyKey()
    {
        _settingsRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppSettings?)null);
        _apiKeyStorageMock
            .Setup(s => s.GetAsync(ApiKeyNames.Gemini, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _handler.Handle(new GetAppSettingsQuery(), CancellationToken.None);

        result.GeminiModelName.Should().Be(AppSettings.DefaultModelName);
        result.GeminiApiKey.Should().BeEmpty();
        result.CalendarFirstDay.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public async Task Handle_WhenNoSettings_AndKeyExists_ReturnsDefaultModelNameAndKey()
    {
        _settingsRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppSettings?)null);
        _apiKeyStorageMock
            .Setup(s => s.GetAsync(ApiKeyNames.Gemini, It.IsAny<CancellationToken>()))
            .ReturnsAsync("my-api-key");

        var result = await _handler.Handle(new GetAppSettingsQuery(), CancellationToken.None);

        result.GeminiModelName.Should().Be(AppSettings.DefaultModelName);
        result.GeminiApiKey.Should().Be("my-api-key");
        result.CalendarFirstDay.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public async Task Handle_WhenSettingsExist_AndNoKey_ReturnsStoredModelNameAndEmptyKey()
    {
        var settings = new AppSettings { GeminiModelName = "gemini-1.5-pro" };
        _settingsRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _apiKeyStorageMock
            .Setup(s => s.GetAsync(ApiKeyNames.Gemini, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _handler.Handle(new GetAppSettingsQuery(), CancellationToken.None);

        result.GeminiModelName.Should().Be("gemini-1.5-pro");
        result.GeminiApiKey.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenSettingsExist_AndKeyExists_ReturnsBothValues()
    {
        var settings = new AppSettings { GeminiModelName = "gemini-1.5-pro" };
        _settingsRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _apiKeyStorageMock
            .Setup(s => s.GetAsync(ApiKeyNames.Gemini, It.IsAny<CancellationToken>()))
            .ReturnsAsync("stored-key");

        var result = await _handler.Handle(new GetAppSettingsQuery(), CancellationToken.None);

        result.GeminiModelName.Should().Be("gemini-1.5-pro");
        result.GeminiApiKey.Should().Be("stored-key");
        result.CalendarFirstDay.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public async Task Handle_WhenSettingsHaveSundayFirstDay_ReturnsSunday()
    {
        var settings = new AppSettings { CalendarFirstDay = DayOfWeek.Sunday };
        _settingsRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _apiKeyStorageMock
            .Setup(s => s.GetAsync(ApiKeyNames.Gemini, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _handler.Handle(new GetAppSettingsQuery(), CancellationToken.None);

        result.CalendarFirstDay.Should().Be(DayOfWeek.Sunday);
    }
}
