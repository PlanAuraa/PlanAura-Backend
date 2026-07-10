using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class ServiceCategoryByIdSpecification : BaseSpecification<ServiceCategory>
{
    public ServiceCategoryByIdSpecification(long id)
        : base(category => category.Id == id)
    {
    }
}
