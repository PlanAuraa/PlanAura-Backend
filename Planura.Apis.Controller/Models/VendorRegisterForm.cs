using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Planura.Core.Application.Abstraction.Vendors.Contracts;
using Planura.Core.Domain.Enums;
using Planura.Shared.Errors.Models;

namespace Planura.Apis.Controller.Models
{
    // Multipart form model — carries IFormFile (an MVC type) and is mapped to the core-safe
    // VendorRegisterRequest before reaching the service layer.
    public class VendorRegisterForm
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = null!;

        [Required, MinLength(8), StringLength(100)]
        public string Password { get; set; } = null!;

        [Required, StringLength(200)]
        public string FullName { get; set; } = null!;

        [Required, Phone, StringLength(20)]
        public string PhoneNumber { get; set; } = null!;

        [Required, StringLength(200)]
        public string BusinessName { get; set; } = null!;

        [StringLength(2000)]
        public string? BusinessDescription { get; set; }

        [Required]
        public VendorBusinessType BusinessType { get; set; }

        [Required]
        public Guid CategoryId { get; set; }

        [Required, StringLength(100)]
        public string City { get; set; } = null!;

        [Required, StringLength(100)]
        public string Area { get; set; } = null!;

        [Required, StringLength(300)]
        public string AddressLine { get; set; } = null!;

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public List<IFormFile> Images { get; set; } = new();

        public List<DocumentType> DocumentTypes { get; set; } = new();
        public List<IFormFile> Documents { get; set; } = new();

        public VendorRegisterRequest ToRequest()
        {
            if (DocumentTypes.Count != Documents.Count)
            {
                throw new BadRequestExeption("Each uploaded document must have a matching document type.");
            }

            return new VendorRegisterRequest
            {
                Email = Email,
                Password = Password,
                FullName = FullName,
                PhoneNumber = PhoneNumber,
                BusinessInfo = new BusinessInfoDto
                {
                    BusinessName = BusinessName,
                    BusinessDescription = BusinessDescription,
                    BusinessType = BusinessType,
                    CategoryId = CategoryId
                },
                Location = new LocationDto
                {
                    City = City,
                    Area = Area,
                    AddressLine = AddressLine,
                    Latitude = Latitude,
                    Longitude = Longitude
                },
                Documents = Documents.Select((file, i) => new VendorDocumentUpload
                {
                    DocumentType = DocumentTypes[i],
                    File = ToUploadedFileInfo(file)
                }).ToList(),
                Images = Images.Select(ToUploadedFileInfo).ToList()
            };
        }

        private static UploadedFileInfo ToUploadedFileInfo(IFormFile file) => new()
        {
            Content = file.OpenReadStream(),
            FileName = file.FileName,
            ContentType = file.ContentType
        };
    }
}
