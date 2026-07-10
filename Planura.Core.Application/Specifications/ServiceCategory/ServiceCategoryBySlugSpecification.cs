using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class ServiceCategoryBySlugSpecification : BaseSpecification<ServiceCategory>
{
    public ServiceCategoryBySlugSpecification(string slug)
        : base(category => category.Slug == slug)
    {
    }
}
