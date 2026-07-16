using Planura.Core.Application.Models.Client;

namespace Planura.Core.Application.Services;

public interface IClientService
{
    Task<ClientProfileDto> GetMyProfileAsync(long userId);
    Task<ClientProfileDto> UpdateMyProfileAsync(long userId, UpdateClientProfileDto dto);
}
