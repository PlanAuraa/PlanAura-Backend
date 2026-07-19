namespace Planura.Core.Application.Models;

public class ReviewSummaryDto
{
    public decimal AvgRating { get; set; }
    public int TotalReviews { get; set; }

    // Count of reviews per star rating (for the ratings breakdown bars).
    public int FiveStar { get; set; }
    public int FourStar { get; set; }
    public int ThreeStar { get; set; }
    public int TwoStar { get; set; }
    public int OneStar { get; set; }
}
