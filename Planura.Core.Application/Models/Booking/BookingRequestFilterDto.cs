using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Models;

public class BookingRequestFilterDto
{
    public BookingStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
