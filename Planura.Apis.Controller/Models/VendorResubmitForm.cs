using Microsoft.AspNetCore.Http;
using Planura.Core.Application.Abstraction.Vendors.Contracts;
using Planura.Core.Domain.Enums;
using Planura.Shared.Errors.Models;

namespace Planura.Apis.Controller.Models
{
    public class VendorResubmitForm
    {
        public List<DocumentType> DocumentTypes { get; set; } = new();
        public List<IFormFile> Documents { get; set; } = new();

        public VendorResubmitRequest ToRequest()
        {
            if (DocumentTypes.Count != Documents.Count)
            {
                throw new BadRequestExeption("Each uploaded document must have a matching document type.");
            }

            return new VendorResubmitRequest
            {
                Documents = Documents.Select((file, i) => new VendorDocumentUpload
                {
                    DocumentType = DocumentTypes[i],
                    File = new UploadedFileInfo
                    {
                        Content = file.OpenReadStream(),
                        FileName = file.FileName,
                        ContentType = file.ContentType
                    }
                }).ToList()
            };
        }
    }
}
