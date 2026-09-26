#nullable enable
using Catalog.Infrastructure;
using CategoryEntity = Catalog.Domain.Category;

namespace Catalog.Features.Category;

/// The Category slice: catalog owns the category taxonomy products hang off.
public sealed class CategoryHandler(CatalogDbContext db)
{
    public async Task<IReadOnlyList<CategoryResponse>> ListAsync(CancellationToken ct) =>
        await db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryResponse(c.Id, c.Name))
            .ToListAsync(ct);

    public async Task<CategoryResponse?> GetAsync(int id, CancellationToken ct) =>
        await db.Categories
            .Where(c => c.Id == id)
            .Select(c => new CategoryResponse(c.Id, c.Name))
            .FirstOrDefaultAsync(ct);

    public async Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken ct)
    {
        var category = new CategoryEntity { Name = request.Name.Trim() };
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);   // local unit of work only — no cross-context write here
        return new CategoryResponse(category.Id, category.Name);
    }
}
