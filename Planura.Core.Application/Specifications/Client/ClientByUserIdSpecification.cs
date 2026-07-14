using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class ClientByUserIdSpecification : BaseSpecification<Client>
{
    public ClientByUserIdSpecification(long userId)
        : base(client => client.UserId == userId)
    {
    }
}
