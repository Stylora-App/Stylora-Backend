using Moq;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Services;
using Xunit;

namespace Stylora.Application.Tests.Services;

public class ExploreServiceTests
{
    private readonly Mock<IAsosService> _asosService = new();
    private readonly List<int> _offsets = [];
    private readonly List<string> _queries = [];
    private readonly ExploreService _service;

    public ExploreServiceTests()
    {
        _service = new ExploreService(_asosService.Object);
    }

    [Fact]
    public async Task SearchAsync_PaletteFilteringLeavesTooFewItems_FillsFirstPageAcrossMultipleRawBatches()
    {
        // Arrange
        SetupBatches(new Dictionary<int, List<ShoppingProductDto>>
        {
            [0] = CreateProducts(1, 10, colour: "orange").Concat(CreateProducts(11, 38, colour: "blue")).ToList(),
            [48] = CreateProducts(49, 15, colour: "orange").Concat(CreateProducts(64, 33, colour: "blue")).ToList(),
        });

        // Act
        var result = await _service.SearchAsync(new ExploreQueryDto { Page = 1, PageSize = 20, Palette = ["#FF8C00"] });

        // Assert
        Assert.Equal(20, result.Products.Count);
        Assert.Equal(Enumerable.Range(1, 10).Concat(Enumerable.Range(49, 10)), result.Products.Select(p => p.Id));
        Assert.True(result.HasMore);
        Assert.All(result.Products, product => Assert.Equal("orange", product.Colour));
        Assert.All(result.Products, product => Assert.True(product.PaletteMatch));
        Assert.Equal([0, 48], _offsets);
    }

    [Fact]
    public async Task SearchAsync_SingleRawBatchSpansTwoPages_ReturnsFilteredLeftoversOnSecondPage()
    {
        // Arrange
        SetupBatches(new Dictionary<int, List<ShoppingProductDto>>
        {
            [0] = CreateProducts(1, 25).Concat(CreateExcludedProducts(26, 23)).ToList(),
            [48] = [],
        });

        // Act
        var result = await _service.SearchAsync(new ExploreQueryDto { Page = 2, PageSize = 20 });

        // Assert
        Assert.Equal([21, 22, 23, 24, 25], result.Products.Select(p => p.Id));
        Assert.False(result.HasMore);
        Assert.Equal([0, 48], _offsets);
    }

    [Fact]
    public async Task SearchAsync_RemainingRawBatchesContainOnlyExcludedItems_SetsHasMoreFalse()
    {
        // Arrange
        SetupBatches(new Dictionary<int, List<ShoppingProductDto>>
        {
            [0] = CreateProducts(1, 20).Concat(CreateExcludedProducts(21, 28)).ToList(),
            [48] = CreateExcludedProducts(49, 48),
            [96] = CreateExcludedProducts(97, 12),
        });

        // Act
        var result = await _service.SearchAsync(new ExploreQueryDto { Page = 1, PageSize = 20 });

        // Assert
        Assert.Equal(20, result.Products.Count);
        Assert.False(result.HasMore);
        Assert.Equal([0, 48, 96], _offsets);
    }

    [Fact]
    public async Task SearchAsync_ConsecutivePages_MaintainFilteredPaginationContinuity()
    {
        // Arrange
        SetupBatches(new Dictionary<int, List<ShoppingProductDto>>
        {
            [0] = CreateProducts(1, 30).Concat(CreateExcludedProducts(31, 18)).ToList(),
            [48] = CreateProducts(49, 15).Concat(CreateExcludedProducts(64, 20)).ToList(),
        });

        // Act
        var pageOne = await _service.SearchAsync(new ExploreQueryDto { Page = 1, PageSize = 20 });
        var pageTwo = await _service.SearchAsync(new ExploreQueryDto { Page = 2, PageSize = 20 });

        // Assert
        Assert.Equal(Enumerable.Range(1, 20), pageOne.Products.Select(p => p.Id));
        Assert.Equal(Enumerable.Range(21, 10).Concat(Enumerable.Range(49, 10)), pageTwo.Products.Select(p => p.Id));
        Assert.True(pageOne.HasMore);
        Assert.True(pageTwo.HasMore);
        Assert.Equal(40, pageOne.Products.Concat(pageTwo.Products).Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public async Task SearchAsync_SearchTermProvided_FiltersResultsWithoutChangingBrowseQuery()
    {
        // Arrange
        SetupBatches(new Dictionary<int, List<ShoppingProductDto>>
        {
            [0] =
            [
                new() { Id = 1, Name = "Blue Jacket", BrandName = "ASOS", Colour = "blue" },
                new() { Id = 2, Name = "Red Dress", BrandName = "ASOS", Colour = "red" },
                new() { Id = 3, Name = "Bikini Red Dress", BrandName = "ASOS", Colour = "red" },
            ],
        });

        // Act
        var result = await _service.SearchAsync(new ExploreQueryDto
        {
            Page = 1,
            PageSize = 20,
            Gender = "women",
            Q = "red dress",
            Palette = ["#FF0000"],
        });

        // Assert
        Assert.Equal([2], result.Products.Select(p => p.Id));
        Assert.Equal(["women fashion outfit"], _queries);
    }

    [Fact]
    public async Task SearchAsync_PaletteDrivenForYouResults_UseBroadBrowseQuery()
    {
        // Arrange
        SetupBatches(new Dictionary<int, List<ShoppingProductDto>>
        {
            [0] = CreateProducts(1, 5, colour: "orange").Concat(CreateProducts(6, 10, colour: "blue")).ToList(),
        });

        // Act
        var result = await _service.SearchAsync(new ExploreQueryDto
        {
            Page = 1,
            PageSize = 20,
            Gender = "women",
            Palette = ["#FF8C00"],
            Season = "True Autumn",
        });

        // Assert
        Assert.Equal([1, 2, 3, 4, 5], result.Products.Select(p => p.Id));
        Assert.Equal(["women fashion outfit"], _queries);
    }

    [Fact]
    public async Task SearchAsync_SubSeasonProvided_UsesCanonicalSeasonVector()
    {
        // Arrange
        SetupBatches(new Dictionary<int, List<ShoppingProductDto>>
        {
            [0] =
            [
                new() { Id = 1, Name = "Forest Jacket", Colour = "olive" },
                new() { Id = 2, Name = "Blue Jacket", Colour = "cobalt blue" },
                new() { Id = 3, Name = "Rust Knit", Colour = "rust" },
            ],
        });

        // Act
        var result = await _service.SearchAsync(new ExploreQueryDto
        {
            Page = 1,
            PageSize = 20,
            Season = "Autumn",
            SubSeason = "Deep Autumn",
        });

        // Assert
        Assert.Equal([1, 3], result.Products.Select(p => p.Id));
        Assert.All(result.Products, product => Assert.True(product.PaletteMatch));
    }

    [Fact]
    public async Task SearchAsync_LaterPages_ReuseCachedFilteredPoolInsteadOfRefetching()
    {
        // Arrange
        SetupBatches(new Dictionary<int, List<ShoppingProductDto>>
        {
            [0] = CreateProducts(1, 30).Concat(CreateExcludedProducts(31, 18)).ToList(),
            [48] = CreateProducts(49, 20).Concat(CreateExcludedProducts(69, 28)).ToList(),
        });

        // Act
        var pageOne = await _service.SearchAsync(new ExploreQueryDto { Page = 1, PageSize = 20 });
        var pageTwo = await _service.SearchAsync(new ExploreQueryDto { Page = 2, PageSize = 20 });

        // Assert
        Assert.Equal(20, pageOne.Products.Count);
        Assert.Equal(20, pageTwo.Products.Count);
        _asosService.Verify(
            s => s.SearchProductsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SearchAsync_SynonymSearchTerms_MatchAcrossProductMetadata()
    {
        // Arrange
        SetupBatches(new Dictionary<int, List<ShoppingProductDto>>
        {
            [0] =
            [
                new() { Id = 1, Name = "Leather Trainers", BrandName = "ASOS", Colour = "white" },
                new() { Id = 2, Name = "Leather Loafers", BrandName = "ASOS", Colour = "white" },
            ],
        });

        // Act
        var result = await _service.SearchAsync(new ExploreQueryDto { Page = 1, PageSize = 20, Q = "sneakers" });

        // Assert
        Assert.Equal([1], result.Products.Select(p => p.Id));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(20, 20)]
    [InlineData(100, 48)]
    public async Task SearchAsync_PageSizeInputVariants_ClampsPageSizeToAsosBatchLimits(int requestedPageSize, int expectedPageSize)
    {
        // Arrange
        SetupBatches([]);

        // Act
        var result = await _service.SearchAsync(new ExploreQueryDto { Page = 0, PageSize = requestedPageSize });

        // Assert
        Assert.Equal(expectedPageSize, result.PageSize);
        Assert.Equal(1, result.Page);
    }

    private void SetupBatches(Dictionary<int, List<ShoppingProductDto>> responses)
        => _asosService
            .Setup(s => s.SearchProductsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Callback<string, int, int>((query, _, offset) =>
            {
                _queries.Add(query);
                _offsets.Add(offset);
            })
            .ReturnsAsync((string _, int _, int offset) => responses.TryGetValue(offset, out var batch) ? batch : []);

    private static List<ShoppingProductDto> CreateProducts(int startId, int count, string colour = "black")
        => Enumerable.Range(startId, count)
            .Select(id => new ShoppingProductDto { Id = id, Name = $"Product {id}", Colour = colour })
            .ToList();

    private static List<ShoppingProductDto> CreateExcludedProducts(int startId, int count)
        => Enumerable.Range(startId, count)
            .Select(id => new ShoppingProductDto { Id = id, Name = $"Bikini {id}", Colour = "black" })
            .ToList();
}

// Covers: Unit, Parameterized, Behaviour, Guard-clause
