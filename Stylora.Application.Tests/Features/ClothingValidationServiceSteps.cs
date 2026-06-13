using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;
using Stylora.Application.Services;
using Stylora.Domain.Enums;
using TechTalk.SpecFlow;

namespace Stylora.Application.Tests.Features;

[Binding]
[Scope(Feature = "Clothing validation service")]
public sealed class ClothingValidationServiceSteps
{
    private Mock<IImageEmbeddingService> _embeddingService = null!;
    private Mock<IClothingReferenceEmbeddingRepository> _referenceRepository = null!;
    private ClothingValidationService _service = null!;
    private ClothingImageValidationResult? _result;

    [BeforeScenario]
    public void Setup()
    {
        _embeddingService = new Mock<IImageEmbeddingService>();
        _referenceRepository = new Mock<IClothingReferenceEmbeddingRepository>();
        _embeddingService
            .Setup(s => s.EmbedImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        _service = new ClothingValidationService(
            _embeddingService.Object,
            _referenceRepository.Object,
            new ClothingValidationSettings { TopK = 5, MinimumClothingShare = 0.6, MinimumMargin = 0.15 },
            NullLogger<ClothingValidationService>.Instance);
    }

    [Given(@"the nearest references contain (\d+) clothing matches at distance ([\d.]+) and (\d+) non-clothing matches at distance ([\d.]+)")]
    public void GivenTheNearestReferences(int clothingCount, string clothingDistance, int nonClothingCount, string nonClothingDistance)
    {
        var matches = Enumerable.Range(0, clothingCount)
            .Select(i => Match(ClothingReferenceLabel.Clothing, $"c{i}", Parse(clothingDistance)))
            .Concat(Enumerable.Range(0, nonClothingCount)
                .Select(i => Match(ClothingReferenceLabel.NonClothing, $"n{i}", Parse(nonClothingDistance))))
            .ToList();
        _referenceRepository
            .Setup(r => r.GetNearestNeighborsAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matches);
    }

    [Given(@"the embedding worker is still starting up")]
    public void GivenTheEmbeddingWorkerIsStillStartingUp()
        => _embeddingService
            .Setup(s => s.EmbedImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Worker startup timed out."));

    [When(@"the image is validated")]
    public async Task WhenTheImageIsValidated()
        => _result = await _service.ValidateAsync("data:image/png;base64,aGVsbG8=");

    [Then(@"the validation status is ""(.*)""")]
    public void ThenTheValidationStatusIs(string status)
        => _result!.Status.Should().Be(Enum.Parse<ClothingValidationStatus>(status));

    [Then(@"the image is considered likely clothing")]
    public void ThenTheImageIsConsideredLikelyClothing() => _result!.IsLikelyClothing.Should().BeTrue();

    [Then(@"the image is not considered likely clothing")]
    public void ThenTheImageIsNotConsideredLikelyClothing() => _result!.IsLikelyClothing.Should().BeFalse();

    [Then(@"the validation message mentions ""(.*)""")]
    public void ThenTheValidationMessageMentions(string text) => _result!.Message.Should().Contain(text);

    private static double Parse(string value) => double.Parse(value, CultureInfo.InvariantCulture);

    private static ClothingReferenceMatch Match(ClothingReferenceLabel label, string sourceKey, double distance)
        => new(label, sourceKey, null, null, null, null, null, null, null, null, null, null, null, distance);
}

// Covers: BDD, Parameterized (Scenario Outline), Behaviour, Guard-clause
