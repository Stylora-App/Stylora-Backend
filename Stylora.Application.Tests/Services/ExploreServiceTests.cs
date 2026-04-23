using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Services;
using Xunit;

namespace Stylora.Application.Tests.Services;

public class ExploreServiceTests
{
    [Fact]
    public async Task SearchAsync_FillsFirstPageAcrossMultipleRawBatches_WhenPaletteFilteringLeavesTooFewItems()
    {
        var firstBatch = CreateProducts(1, 10, colour: "orange")
            .Concat(CreateProducts(11, 38, colour: "blue"))
            .ToList();
        var secondBatch = CreateProducts(49, 15, colour: "orange")
            .Concat(CreateProducts(64, 33, colour: "blue"))
            .ToList();

        var asosService = new FakeAsosService(new Dictionary<int, List<ShoppingProductDto>>
        {
            [0] = firstBatch,
            [48] = secondBatch,
        });
        var service = new ExploreService(asosService);

        var result = await service.SearchAsync(new ExploreQueryDto
        {
            Page = 1,
            PageSize = 20,
            Palette = ["#FF8C00"],
        });

        Assert.Equal(20, result.Products.Count);
        Assert.Equal(Enumerable.Range(1, 10).Concat(Enumerable.Range(49, 10)), result.Products.Select(p => p.Id));
        Assert.True(result.HasMore);
        Assert.All(result.Products, product => Assert.Equal("orange", product.Colour));
        Assert.All(result.Products, product => Assert.True(product.PaletteMatch));
        Assert.Equal([0, 48], asosService.Offsets);
    }

    [Fact]
    public async Task SearchAsync_ReturnsFilteredLeftoversOnSecondPage_WhenSingleRawBatchContainsMoreThanOnePage()
    {
        var firstBatch = CreateProducts(1, 25)
            .Concat(CreateExcludedProducts(26, 23))
            .ToList();

        var asosService = new FakeAsosService(new Dictionary<int, List<ShoppingProductDto>>
        {
            [0] = firstBatch,
            [48] = [],
        });
        var service = new ExploreService(asosService);

        var result = await service.SearchAsync(new ExploreQueryDto
        {
            Page = 2,
            PageSize = 20,
        });

        Assert.Equal([21, 22, 23, 24, 25], result.Products.Select(p => p.Id));
        Assert.False(result.HasMore);
        Assert.Equal([0, 48], asosService.Offsets);
    }

    [Fact]
    public async Task SearchAsync_SetsHasMoreFalse_WhenMoreRawBatchesExistButNoMoreFilteredItemsRemain()
    {
        var firstBatch = CreateProducts(1, 20)
            .Concat(CreateExcludedProducts(21, 28))
            .ToList();
        var secondBatch = CreateExcludedProducts(49, 48);
        var thirdBatch = CreateExcludedProducts(97, 12);

        var asosService = new FakeAsosService(new Dictionary<int, List<ShoppingProductDto>>
        {
            [0] = firstBatch,
            [48] = secondBatch,
            [96] = thirdBatch,
        });
        var service = new ExploreService(asosService);

        var result = await service.SearchAsync(new ExploreQueryDto
        {
            Page = 1,
            PageSize = 20,
        });

        Assert.Equal(20, result.Products.Count);
        Assert.False(result.HasMore);
        Assert.Equal([0, 48, 96], asosService.Offsets);
    }

    [Fact]
    public async Task SearchAsync_MaintainsFilteredPaginationContinuityAcrossPages()
    {
        var firstBatch = CreateProducts(1, 30)
            .Concat(CreateExcludedProducts(31, 18))
            .ToList();
        var secondBatch = CreateProducts(49, 15)
            .Concat(CreateExcludedProducts(64, 20))
            .ToList();

        var pageOneAsosService = new FakeAsosService(new Dictionary<int, List<ShoppingProductDto>>
        {
            [0] = firstBatch,
            [48] = secondBatch,
        });
        var pageTwoAsosService = new FakeAsosService(new Dictionary<int, List<ShoppingProductDto>>
        {
            [0] = firstBatch,
            [48] = secondBatch,
        });
        var serviceForPageOne = new ExploreService(pageOneAsosService);
        var serviceForPageTwo = new ExploreService(pageTwoAsosService);

        var pageOne = await serviceForPageOne.SearchAsync(new ExploreQueryDto
        {
            Page = 1,
            PageSize = 20,
        });
        var pageTwo = await serviceForPageTwo.SearchAsync(new ExploreQueryDto
        {
            Page = 2,
            PageSize = 20,
        });

        Assert.Equal(20, pageOne.Products.Count);
        Assert.Equal(20, pageTwo.Products.Count);
        Assert.Equal(Enumerable.Range(1, 20), pageOne.Products.Select(p => p.Id));
        Assert.Equal(Enumerable.Range(21, 10).Concat(Enumerable.Range(49, 10)), pageTwo.Products.Select(p => p.Id));
        Assert.True(pageOne.HasMore);
        Assert.True(pageTwo.HasMore);
        Assert.Equal(40, pageOne.Products.Select(p => p.Id).Concat(pageTwo.Products.Select(p => p.Id)).Distinct().Count());
    }

    [Fact]
    public async Task SearchAsync_AppliesSearchWithinFilteredResults_WithoutChangingBrowseQuery()
    {
        var firstBatch = new List<ShoppingProductDto>
        {
            new() { Id = 1, Name = "Blue Jacket", BrandName = "ASOS", Colour = "blue" },
            new() { Id = 2, Name = "Red Dress", BrandName = "ASOS", Colour = "red" },
            new() { Id = 3, Name = "Bikini Red Dress", BrandName = "ASOS", Colour = "red" },
        };

        var asosService = new FakeAsosService(new Dictionary<int, List<ShoppingProductDto>>
        {
            [0] = firstBatch,
        });
        var service = new ExploreService(asosService);

        var result = await service.SearchAsync(new ExploreQueryDto
        {
            Page = 1,
            PageSize = 20,
            Gender = "women",
            Q = "red dress",
            Palette = ["#FF0000"],
        });

        Assert.Equal([2], result.Products.Select(p => p.Id));
        Assert.Equal(["women fashion outfit"], asosService.Queries);
    }

    [Fact]
    public async Task SearchAsync_UsesBroadBrowseQuery_ForPaletteDrivenForYouResults()
    {
        var firstBatch = CreateProducts(1, 5, colour: "orange")
            .Concat(CreateProducts(6, 10, colour: "blue"))
            .ToList();

        var asosService = new FakeAsosService(new Dictionary<int, List<ShoppingProductDto>>
        {
            [0] = firstBatch,
        });
        var service = new ExploreService(asosService);

        var result = await service.SearchAsync(new ExploreQueryDto
        {
            Page = 1,
            PageSize = 20,
            Gender = "women",
            Palette = ["#FF8C00"],
            Season = "True Autumn",
        });

        Assert.Equal([1, 2, 3, 4, 5], result.Products.Select(p => p.Id));
        Assert.Equal(["women fashion outfit"], asosService.Queries);
    }

    private static List<ShoppingProductDto> CreateProducts(int startId, int count, string colour = "black")
        => Enumerable.Range(startId, count)
            .Select(id => new ShoppingProductDto
            {
                Id = id,
                Name = $"Product {id}",
                Colour = colour,
            })
            .ToList();

    private static List<ShoppingProductDto> CreateExcludedProducts(int startId, int count)
        => Enumerable.Range(startId, count)
            .Select(id => new ShoppingProductDto
            {
                Id = id,
                Name = $"Bikini {id}",
                Colour = "black",
            })
            .ToList();

    private sealed class FakeAsosService : IAsosService
    {
        private readonly Dictionary<int, List<ShoppingProductDto>> _responses;

        public FakeAsosService(Dictionary<int, List<ShoppingProductDto>> responses)
        {
            _responses = responses;
        }

        public List<int> Offsets { get; } = [];
        public List<string> Queries { get; } = [];

        public Task<List<ShoppingProductDto>> SearchProductsAsync(string query, int limit, int offset)
        {
            Queries.Add(query);
            Offsets.Add(offset);
            return Task.FromResult(_responses.TryGetValue(offset, out var response) ? response : []);
        }
    }
}
