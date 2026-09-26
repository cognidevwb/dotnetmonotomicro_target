#nullable enable
namespace Catalog.Features.Category;

public sealed record CreateCategoryRequest(string Name);

public sealed record CategoryResponse(int Id, string Name);

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
    }
}
