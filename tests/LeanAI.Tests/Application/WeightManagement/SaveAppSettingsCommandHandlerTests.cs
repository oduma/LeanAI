using FluentAssertions;
using LeanAI.Application.Common;
using LeanAI.Application.WeightManagement.Commands.SaveAppSettings;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class SaveAppSettingsCommandHandlerTests
{
    private readonly Mock<IAppSettingsRepository> _settingsRepoMock = new();
    private readonly Mock<IApiKeyStorage> _apiKeyStorageMock = new();
    private readonly SaveAppSettingsCommandHandler _handler;

    public SaveAppSettingsCommandHandlerTests()
    {
        _handler = new SaveAppSettingsCommandHandler(_settingsRepoMock.Object, _apiKeyStorageMock.Object);
    }

    [Fact]
    public async Task Handle_WhenNoExistingSettings_CreatesNewEntityWithModelName_SavesAndSetsKey()
    {
        AppSettings? savedSettings = null;
        _settingsRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppSettings?)null);
        _settingsRepoMock
            .Setup(r => r.SaveAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .Callback<AppSettings, CancellationToken>((s, _) => savedSettings = s)
            .Returns(Task.CompletedTask);
        _apiKeyStorageMock
            .Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new SaveAppSettingsCommand("gemini-1.5-pro", "new-api-key", DayOfWeek.Monday, UseBmr: false);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().Be(Unit.Value);
        savedSettings.Should().NotBeNull();
        savedSettings!.GeminiModelName.Should().Be("gemini-1.5-pro");
        savedSettings.CalendarFirstDay.Should().Be(DayOfWeek.Monday);
        savedSettings.UseBmr.Should().BeFalse();
        _settingsRepoMock.Verify(r => r.SaveAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()), Times.Once);
        _apiKeyStorageMock.Verify(s => s.SetAsync(ApiKeyNames.Gemini, "new-api-key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSettingsExist_UpdatesModelName_SavesAndSetsKey()
    {
        var existingSettings = new AppSettings { GeminiModelName = "old-model" };
        AppSettings? savedSettings = null;
        _settingsRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSettings);
        _settingsRepoMock
            .Setup(r => r.SaveAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .Callback<AppSettings, CancellationToken>((s, _) => savedSettings = s)
            .Returns(Task.CompletedTask);
        _apiKeyStorageMock
            .Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new SaveAppSettingsCommand("gemini-2.5-flash", "updated-key", DayOfWeek.Sunday, UseBmr: true);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().Be(Unit.Value);
        savedSettings.Should().BeSameAs(existingSettings);
        savedSettings!.GeminiModelName.Should().Be("gemini-2.5-flash");
        savedSettings.CalendarFirstDay.Should().Be(DayOfWeek.Sunday);
        savedSettings.UseBmr.Should().BeTrue();
        _settingsRepoMock.Verify(r => r.SaveAsync(existingSettings, It.IsAny<CancellationToken>()), Times.Once);
        _apiKeyStorageMock.Verify(s => s.SetAsync(ApiKeyNames.Gemini, "updated-key", It.IsAny<CancellationToken>()), Times.Once);
    }
}
