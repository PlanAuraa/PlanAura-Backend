using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class ReviewByIdWithResponseSpecification : BaseSpecification<Review>
{
    public ReviewByIdWithResponseSpecification(long reviewId)
        : base(review => review.Id == reviewId)
    {
        AddInclude(review => review.Response!);
    }
}
