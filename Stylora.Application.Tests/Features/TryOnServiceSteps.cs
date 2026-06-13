using FluentAssertions;
using Moq;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Services;
using Stylora.Domain.Entities;
using TechTalk.SpecFlow;

namespace Stylora.Application.Tests.Features;

[Binding]
[Scope(Feature = "Virtual try-on service")]
public sealed class TryOnServiceSteps
{
    private Mock<IGeminiService> _gemini = null!;
    private Mock<ITryOnRepository> _tryOnRepository = null!;
    private TryOnService _service = null!;
    private TryOnResponse? _response;
    private Exception? _error;

    [BeforeScenario]
    public void Setup()
    {
        _gemini = new Mock<IGeminiService>();
        _tryOnRepository = new Mock<ITryOnRepository>();
        _tryOnRepository
            .Setup(r => r.CreateAsync(It.IsAny<TryOnSession>()))
            .ReturnsAsync((TryOnSession session) => session);
        _tryOnRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TryOnSession>()))
            .ReturnsAsync((TryOnSession session) => session);
        _service = new TryOnService(_gemini.Object, _tryOnRepository.Object);
    }

    [Given(@"the AI model generates the image ""(.*)""")]
    public void GivenTheAiModelGeneratesTheImage(string generatedImage)
        => _gemini
            .Setup(g => g.GenerateTryOnAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(generatedImage);

    [Given(@"the AI model fails with ""(.*)""")]
    public void GivenTheAiModelFailsWith(string message)
        => _gemini
            .Setup(g => g.GenerateTryOnAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException(message));

    [When(@"a try-on is generated for person ""(.*)"" and clothing ""(.*)""")]
    public async Task WhenATryOnIsGenerated(string personImage, string clothingImage)
    {
        try
        {
            _response = await _service.GenerateTryOnAsync(
                new TryOnRequest { PersonImageBase64 = personImage, ClothingImageBase64 = clothingImage },
                Guid.NewGuid());
        }
        catch (Exception exception)
        {
            _error = exception;
        }
    }

    [Then(@"the generated image is ""(.*)""")]
    public void ThenTheGeneratedImageIs(string generatedImage) => _response!.GeneratedImage.Should().Be(generatedImage);

    [Then(@"the session is persisted as successful")]
    public void ThenTheSessionIsPersistedAsSuccessful()
        => _tryOnRepository.Verify(r => r.UpdateAsync(It.Is<TryOnSession>(s => s.IsSuccessful)), Times.Once);

    [Then(@"the try-on fails with ""(.*)""")]
    public void ThenTheTryOnFailsWith(string message)
    {
        _error.Should().BeOfType<InvalidOperationException>();
        _error!.Message.Should().Be(message);
    }

    [Then(@"the session is persisted as failed with message ""(.*)""")]
    public void ThenTheSessionIsPersistedAsFailed(string message)
        => _tryOnRepository.Verify(
            r => r.UpdateAsync(It.Is<TryOnSession>(s => !s.IsSuccessful && s.ErrorMessage == message)),
            Times.Once);

    [Then(@"the model received person ""(.*)"" and clothing ""(.*)""")]
    public void ThenTheModelReceivedImages(string personImage, string clothingImage)
        => _gemini.Verify(g => g.GenerateTryOnAsync(personImage, clothingImage), Times.Once);
}

// Covers: BDD, Parameterized (Scenario Outline), Behaviour, Error-path
