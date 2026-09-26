#nullable enable
using Catalog.Infrastructure;
using ProductEntity = Catalog.Domain.Product;

namespace Catalog.Features.Product;

/// The Product slice: the catalogue of sellable products and the price other
/// contexts read (orders calls the price route through its typed CatalogClient).
public sealed class ProductHandler(CatalogDbContext db, CatalogService catalog)
{
    public async Task<IReadOnlyList<ProductResponse>> ListAsync(int? categoryId, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking();
        if (categoryId is > 0)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }

        return await query
            .OrderBy(p => p.Id)
            .Select(p => new ProductResponse(p.Id, p.Name, p.Price, p.CategoryId))
            .ToListAsync(ct);
    }

    /// Ported from the monolith's CatalogService.GetProduct.
    public async Task<ProductResponse?> GetAsync(int id, CancellationToken ct)
    {
        var product = await catalog.GetProduct(id);
        return product is null
            ? null
            : new ProductResponse(product.Id, product.Name, product.Price, product.CategoryId);
    }

    /// Ported from the monolith's CatalogService.PriceOf — the cross-context read model.
    public async Task<ProductPriceResponse?> GetPriceAsync(int id, CancellationToken ct)
    {
        var product = await catalog.GetProduct(id);
        return product is null ? null : new ProductPriceResponse(product.Id, product.Price);
    }

    public async Task<ProductResponse?> CreateAsync(CreateProductRequest request, CancellationToken ct)
    {
        var categoryExists = await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct);
        if (!categoryExists)
        {
            return null;
        }

        var product = new ProductEntity
        {
            Name = request.Name.Trim(),
            Price = request.Price,
            CategoryId = request.CategoryId
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);   // local unit of work only
        return new ProductResponse(product.Id, product.Name, product.Price, product.CategoryId);
    }
}
