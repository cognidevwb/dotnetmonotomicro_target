#nullable enable
namespace Catalog.Features.Product;

public sealed record CreateProductRequest(string Name, decimal Price, int CategoryId);

public sealed record ProductResponse(int Id, string Name, decimal Price, int CategoryId);

public sealed record ProductPriceResponse(int ProductId, decimal Price);

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Price).GreaterThanOrEqualTo(0m);
        RuleFor(r => r.CategoryId).GreaterThan(0);
    }
}
