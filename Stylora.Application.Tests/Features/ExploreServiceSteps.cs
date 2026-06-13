using FluentAssertions;
using Moq;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Services;
using TechTalk.SpecFlow;

namespace Stylora.Application.Tests.Features;

[Binding]
[Scope(Feature = "Explore service")]
public sealed class ExploreServiceSteps
{
    private Mock<IAsosService> _asosService = null!;
    private ExploreService _service = null!;
    private List<string>? _palette;
    private ExploreResultDto? _result;

    [BeforeScenario]
    public void Setup()
    {
        _asosService = new Mock<IAsosService>();
        _service = new ExploreService(_asosService.Object);
    }

    [Given(@"the product catalogue returns:")]
    public void GivenTheProductCatalogueReturns(Table table)
    {
        var products = table.Rows
            .Select(row => new ShoppingProductDto
            {
                Id = int.Parse(row["id"]),
                Name = row["name"],
                Colour = row["colour"]
            })
            .ToList();
        _asosService
            .Setup(s => s.SearchProductsAsync(It.IsAny<string>(), It.IsAny<int>(), 0))
            .ReturnsAsync(products);
        _asosService
            .Setup(s => s.SearchProductsAsync(It.IsAny<string>(), It.IsAny<int>(), It.Is<int>(offset => offset > 0)))
            .ReturnsAsync([]);
    }

    [Given(@"the product catalogue is empty")]
    public void GivenTheProductCatalogueIsEmpty()
        => _asosService
            .Setup(s => s.SearchProductsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync([]);

    [Given(@"the shopper palette is ""(.*)""")]
    public void GivenTheShopperPaletteIs(string hex) => _palette = [hex];

    [When(@"products are searched on page (\d+) with page size (-?\d+)")]
    public async Task WhenProductsAreSearched(int page, int pageSize)
        => _result = await _service.SearchAsync(new ExploreQueryDto
        {
            Page = page,
            PageSize = pageSize,
            Palette = _palette
        });

    [Then(@"only the product ids ""(.*)"" are returned")]
    public void ThenOnlyTheProductIdsAreReturned(string expectedIds)
    {
        var expected = expectedIds.Split(',').Select(int.Parse).ToList();
        _result!.Products.Select(p => p.Id).Should().Equal(expected);
    }

    [Then(@"every returned product is a palette match")]
    public void ThenEveryReturnedProductIsAPaletteMatch()
        => _result!.Products.Should().OnlyContain(product => product.PaletteMatch);

    [Then(@"the catalogue was queried exactly (\d+) time")]
    public void ThenTheCatalogueWasQueried(int times)
        => _asosService.Verify(
            s => s.SearchProductsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Exactly(times));

    [Then(@"the result page size is (\d+)")]
    public void ThenTheResultPageSizeIs(int pageSize) => _result!.PageSize.Should().Be(pageSize);
}

// Covers: BDD, Parameterized (Scenario Outline), Behaviour, Guard-clause
