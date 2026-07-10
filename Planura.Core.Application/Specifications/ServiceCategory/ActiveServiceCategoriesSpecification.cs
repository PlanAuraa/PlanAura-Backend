using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class ActiveServiceCategoriesSpecification : BaseSpecification<ServiceCategory>
{
    public ActiveServiceCategoriesSpecification()
        : base(category => category.IsActive)
    {
        ApplyOrderBy(category => category.NameEn);
    }
}
