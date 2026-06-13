using FluentAssertions;
using Moq;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;
using Stylora.Application.Services;
using Stylora.Domain.Entities;
using Stylora.Domain.Enums;
using TechTalk.SpecFlow;

namespace Stylora.Application.Tests.Features;

[Binding]
[Scope(Feature = "Wardrobe service")]
public sealed class WardrobeServiceSteps
{
    private const string Image = "data:image/png;base64,aGVsbG8=";

    private Mock<IWardrobeRepository> _repository = null!;
    private Mock<IClothingValidationService> _validator = null!;
    private WardrobeService _service = null!;
    private WardrobeItem? _savedItem;
    private CreateWardrobeItemResponse? _response;

    [BeforeScenario]
    public void Setup()
    {
        _repository = new Mock<IWardrobeRepository>();
        _validator = new Mock<IClothingValidationService>();
        _repository
            .Setup(r => r.AddItemAsync(It.IsAny<string>(), It.IsAny<WardrobeItem>()))
            .Callback<string, WardrobeItem>((_, item) =>
            {
                item.Id = Guid.NewGuid();
                _savedItem = item;
            })
            .ReturnsAsync((string _, WardrobeItem item) => item);
        _repository
            .Setup(r => r.ResolveColorAsync(It.IsAny<string?>()))
            .ReturnsAsync((string? name) => string.IsNullOrWhiteSpace(name) ? null : new Color { Name = name });
        _service = new WardrobeService(_repository.Object, _validator.Object);
    }

    [Given(@"the clothing validator approves the image")]
    public void GivenTheValidatorApprovesTheImage()
        => SetupValidation(new ClothingImageValidationResult
        {
            Status = ClothingValidationStatus.Pass,
            IsLikelyClothing = true,
            Confidence = 0.93,
            Message = "ok",
            NearestLabels = ["clothing"]
        });

    [Given(@"the clothing validator flags the image with a warning")]
    public void GivenTheValidatorFlagsTheImage()
        => SetupValidation(new ClothingImageValidationResult
        {
            Status = ClothingValidationStatus.Warning,
            IsLikelyClothing = false,
            Confidence = 0.81,
            Message = "warning",
            NearestLabels = ["non_clothing"]
        });

    [When(@"a wardrobe item is added")]
    public async Task WhenAWardrobeItemIsAdded()
        => _response = await _service.AddItemAsync(Guid.NewGuid().ToString(), new CreateWardrobeItemRequest { Image = Image });

    [When(@"a wardrobe item is added with the warning overridden")]
    public async Task WhenAWardrobeItemIsAddedWithOverride()
        => _response = await _service.AddItemAsync(Guid.NewGuid().ToString(), new CreateWardrobeItemRequest
        {
            Image = Image,
            OverrideValidationWarning = true
        });

    [When(@"a wardrobe item is added with category ""(.*)""")]
    public async Task WhenAWardrobeItemIsAddedWithCategory(string category)
        => _response = await _service.AddItemAsync(Guid.NewGuid().ToString(), new CreateWardrobeItemRequest
        {
            Image = Image,
            Category = category
        });

    [Then(@"the item is saved to the wardrobe")]
    public void ThenTheItemIsSaved()
    {
        _response!.Item.Should().NotBeNull();
        _repository.Verify(r => r.AddItemAsync(It.IsAny<string>(), It.IsAny<WardrobeItem>()), Times.Once);
    }

    [Then(@"the item is not saved")]
    public void ThenTheItemIsNotSaved()
    {
        _response!.Item.Should().BeNull();
        _repository.Verify(r => r.AddItemAsync(It.IsAny<string>(), It.IsAny<WardrobeItem>()), Times.Never);
    }

    [Then(@"the item validation status is ""(.*)""")]
    public void ThenTheItemValidationStatusIs(string status) => _response!.Validation!.Status.Should().Be(status);

    [Then(@"the response offers an override")]
    public void ThenTheResponseOffersAnOverride() => _response!.Validation!.CanOverride.Should().BeTrue();

    [Then(@"the saved item category is ""(.*)""")]
    public void ThenTheSavedItemCategoryIs(string category) => _savedItem!.Category.ToString().Should().Be(category);

    private void SetupValidation(ClothingImageValidationResult result)
        => _validator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
}

// Covers: BDD, Parameterized (Scenario Outline), Behaviour, Guard-clause
