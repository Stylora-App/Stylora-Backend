using FluentAssertions;
using Moq;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Services;
using Stylora.Domain.Entities;
using Stylora.Domain.Enums;
using TechTalk.SpecFlow;

namespace Stylora.Application.Tests.Features;

[Binding]
[Scope(Feature = "User profile service")]
public sealed class UserServiceSteps
{
    private Mock<IUserRepository> _userRepository = null!;
    private UserService _service = null!;
    private string _userId = string.Empty;
    private UserProfileDto? _profile;

    [BeforeScenario]
    public void Setup()
    {
        _userRepository = new Mock<IUserRepository>();
        _service = new UserService(_userRepository.Object);
    }

    [Given(@"a stored user named ""(.*)"" with season ""(.*)"" and sub-season ""(.*)""")]
    public void GivenAStoredUser(string firstName, string season, string subSeason)
    {
        var id = Guid.NewGuid();
        _userId = id.ToString();
        _userRepository
            .Setup(r => r.GetByIdWithAnalysisAsync(id))
            .ReturnsAsync(new User
            {
                Id = id,
                FirstName = firstName,
                Style = StylePreference.Casual,
                ColorAnalysisResult = new SeasonAnalysisResult { Season = season, SubSeason = subSeason }
            });
    }

    [Given(@"the requesting user id is ""(.*)""")]
    public void GivenTheRequestingUserIdIs(string userId) => _userId = userId;

    [When(@"the profile is requested")]
    public async Task WhenTheProfileIsRequested() => _profile = await _service.GetProfileAsync(_userId);

    [Then(@"the profile first name is ""(.*)""")]
    public void ThenTheProfileFirstNameIs(string firstName) => _profile!.FirstName.Should().Be(firstName);

    [Then(@"the profile undertone is ""(.*)""")]
    public void ThenTheProfileUndertoneIs(string undertone) => _profile!.Undertone.Should().Be(undertone);

    [Then(@"the profile contrast is ""(.*)""")]
    public void ThenTheProfileContrastIs(string contrast) => _profile!.Contrast.Should().Be(contrast);

    [Then(@"the profile is empty")]
    public void ThenTheProfileIsEmpty()
    {
        _profile!.FirstName.Should().BeNull();
        _profile.Season.Should().BeNull();
        _profile.Palette.Should().BeEmpty();
    }

    [Then(@"the user repository was never queried")]
    public void ThenTheUserRepositoryWasNeverQueried()
        => _userRepository.Verify(r => r.GetByIdWithAnalysisAsync(It.IsAny<Guid>()), Times.Never);
}

// Covers: BDD, Parameterized (Scenario Outline), Behaviour, Guard-clause
