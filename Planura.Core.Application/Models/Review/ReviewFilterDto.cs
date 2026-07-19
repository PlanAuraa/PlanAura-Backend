namespace Planura.Core.Application.Models;

public class ReviewFilterDto
{
    public int? MinRating { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
