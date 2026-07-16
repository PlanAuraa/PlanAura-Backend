using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

/// <summary>
/// Same lookup as <see cref="ClientByUserIdSpecification"/> but also eager-loads
/// the ApplicationUser navigation, since the "My Profile" DTO needs
/// FullName/Email/PhoneNumber alongside the Client entity's own fields.
/// </summary>
public class ClientProfileByUserIdSpecification : BaseSpecification<Client>
{
    public ClientProfileByUserIdSpecification(long userId)
        : base(client => client.UserId == userId)
    {
        AddInclude(client => client.User);
    }
}
