using Microsoft.AspNetCore.Identity;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Models.Client;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services;

/// <summary>
/// Backs the client-facing "My Profile" page. Mirrors VendorService's
/// GetByIdAsync/UpdateProfileAsync shape, but Client is split across two
/// tables (ApplicationUser for identity fields, Client for the rest), so
/// updates touch both: UserManager for FullName/Email/PhoneNumber, and the
/// generic repository for City/DateOfBirth/AvatarUrl.
/// </summary>
public class ClientService : IClientService
{
    private const string ClientAvatarFolder = "client-avatars";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAttachmentService _attachmentService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ClientService(
        IUnitOfWork unitOfWork,
        IAttachmentService attachmentService,
        UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _attachmentService = attachmentService;
        _userManager = userManager;
    }

    public async Task<ClientProfileDto> GetMyProfileAsync(long userId)
    {
        var client = await _unitOfWork.Repository<Client, long>()
            .GetWithSpecAsync(new ClientProfileByUserIdSpecification(userId));

        if (client is null)
        {
            throw new NotFoundExeption(nameof(Client), userId);
        }

        return ResolveImageUrl(MapToDto(client));
    }

    public async Task<ClientProfileDto> UpdateMyProfileAsync(long userId, UpdateClientProfileDto dto)
    {
        var repository = _unitOfWork.Repository<Client, long>();
        var client = await repository.GetWithSpecAsync(new ClientProfileByUserIdSpecification(userId));

        if (client is null)
        {
            throw new NotFoundExeption(nameof(Client), userId);
        }

        if (string.IsNullOrWhiteSpace(dto.FullName))
        {
            throw new BadRequestExeption("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            throw new BadRequestExeption("Email is required.");
        }

        client.User.FullName = dto.FullName;
        client.User.Email = dto.Email;
        client.User.UserName = dto.Email;
        client.User.PhoneNumber = dto.PhoneNumber;

        var identityResult = await _userManager.UpdateAsync(client.User);
        if (!identityResult.Succeeded)
        {
            throw new BadRequestExeption(
                string.Join("; ", identityResult.Errors.Select(error => $"{error.Code}: {error.Description}")));
        }

        client.City = dto.City;
        client.DateOfBirth = dto.DateOfBirth;

        if (dto.AvatarFile is not null && dto.AvatarFile.Length > 0)
        {
            _attachmentService.Delete(client.AvatarUrl ?? string.Empty);
            client.AvatarUrl = await _attachmentService.UploadAsynce(dto.AvatarFile, ClientAvatarFolder);
        }

        client.UpdatedAt = DateTimeOffset.UtcNow;
        repository.Update(client);
        await _unitOfWork.SaveChangesAsync();

        // Re-fetch so the returned DTO reflects the committed User + Client state together.
        return await GetMyProfileAsync(userId);
    }

    private static ClientProfileDto MapToDto(Client client) => new()
    {
        Id = client.Id,
        FullName = client.User.FullName,
        Email = client.User.Email!,
        PhoneNumber = client.User.PhoneNumber,
        City = client.City,
        DateOfBirth = client.DateOfBirth,
        AvatarUrl = client.AvatarUrl,
        CreatedAt = client.CreatedAt
    };

    private ClientProfileDto ResolveImageUrl(ClientProfileDto dto)
    {
        dto.AvatarUrl = _attachmentService.ToAbsoluteUrl(dto.AvatarUrl);
        return dto;
    }
}
