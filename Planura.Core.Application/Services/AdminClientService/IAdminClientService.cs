using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminClient;

namespace Planura.Core.Application.Services.AdminClient
{
    public interface IAdminClientService
    {
        Task<PagedResult<AdminClientListItemDto>> ListClientsAsync(AdminClientFilterDto filter);

        Task<AdminClientDetailsDto> GetClientDetailsAsync(long clientId);
    }
}
