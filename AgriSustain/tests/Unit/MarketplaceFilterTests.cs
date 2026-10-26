using AgriSustain.Application.DTOs.Marketplace;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using Xunit;

namespace AgriSustain.UnitTests;

public sealed class MarketplaceFilterTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(1L, null)]
    [InlineData(null, 2L)]
    [InlineData(1L, 2L)]
    public async Task GetFilters_ValidScope_ForwardsCategoryAndSupplier(long? category, long? supplier)
    {
        var repository = new Filters();
        var service = new MarketplaceService(repository);
        using var cancellation = new CancellationTokenSource();

        var result = await service.GetFiltersAsync(cancellation.Token, category, supplier);

        Assert.True(result.IsSuccess);
        Assert.Same(repository.Response, result.Value);
        Assert.Equal(category, repository.CategoryId);
        Assert.Equal(supplier, repository.SupplierId);
        Assert.Equal(cancellation.Token, repository.Cancellation);
        Assert.Equal(1, repository.Calls);
    }

    [Theory]
    [InlineData(0L, null)]
    [InlineData(-1L, null)]
    [InlineData(null, 0L)]
    [InlineData(null, -1L)]
    public async Task GetFilters_InvalidScope_RejectsBeforeQuery(long? category, long? supplier)
    {
        var repository = new Filters();
        var result = await new MarketplaceService(repository).GetFiltersAsync(
            default, category, supplier);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(0, repository.Calls);
    }

    private sealed class Filters : IMarketplaceRepository
    {
        public MarketplaceFiltersResponse Response { get; } = new([], [], [], [], []);
        public long? CategoryId { get; private set; }
        public long? SupplierId { get; private set; }
        public CancellationToken Cancellation { get; private set; }
        public int Calls { get; private set; }

        public Task<MarketplaceFiltersResponse> GetFiltersAsync(CancellationToken cancellationToken,
            long? categoryId = null, long? supplierId = null)
        {
            CategoryId = categoryId;
            SupplierId = supplierId;
            Cancellation = cancellationToken;
            Calls++;
            return Task.FromResult(Response);
        }

        public Task<MarketplacePageResponse> BrowseAsync(BrowseMarketplaceRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProductDetailResponse?> GetDetailAsync(long productId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
