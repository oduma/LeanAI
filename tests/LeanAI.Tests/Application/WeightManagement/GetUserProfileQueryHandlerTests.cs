using AutoMapper;
using FluentAssertions;
using LeanAI.Application.WeightManagement.DTOs;
using LeanAI.Application.WeightManagement.Queries.GetUserProfile;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Enums;
using LeanAI.Domain.WeightManagement.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class GetUserProfileQueryHandlerTests
{
    private readonly Mock<IUserProfileRepository> _repositoryMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly GetUserProfileQueryHandler _handler;

    public GetUserProfileQueryHandlerTests()
    {
        _handler = new GetUserProfileQueryHandler(_repositoryMock.Object, _mapperMock.Object);
    }

    [Fact]
    public async Task Handle_WhenNoProfile_ReturnsNull()
    {
        _repositoryMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);

        var result = await _handler.Handle(new GetUserProfileQuery(), CancellationToken.None);

        result.Should().BeNull();
        _mapperMock.Verify(m => m.Map<UserProfileDto>(It.IsAny<UserProfile>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProfileExists_CallsMapperAndReturnsDto()
    {
        var profile = new UserProfile
        {
            Gender           = Gender.Male,
            Age              = 35,
            HeightCm         = 180.0,
            StartingWeightKg = 85.0,
            TargetWeightKg   = 75.0,
            TargetPeriod     = TargetPeriod.OneYear,
            UnitSystem       = UnitSystem.Metric
        };
        var expectedDto = new UserProfileDto(profile.Id, UnitSystem.Metric, Gender.Male, 35, 180.0, 85.0, 75.0, TargetPeriod.OneYear, true);

        _repositoryMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _mapperMock
            .Setup(m => m.Map<UserProfileDto>(profile))
            .Returns(expectedDto);

        var result = await _handler.Handle(new GetUserProfileQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Be(expectedDto);
        _mapperMock.Verify(m => m.Map<UserProfileDto>(profile), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenProfileExists_MapperIsCalledExactlyOnce()
    {
        var profile = new UserProfile { UnitSystem = UnitSystem.Imperial };
        var dto = new UserProfileDto(profile.Id, UnitSystem.Imperial, null, null, null, null, null, null, false);

        _repositoryMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _mapperMock
            .Setup(m => m.Map<UserProfileDto>(profile))
            .Returns(dto);

        await _handler.Handle(new GetUserProfileQuery(), CancellationToken.None);

        _mapperMock.Verify(m => m.Map<UserProfileDto>(It.IsAny<UserProfile>()), Times.Once);
    }
}
