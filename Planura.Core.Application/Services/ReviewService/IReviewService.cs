using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services;

public interface IReviewService
{
    Task<PagedResult<ReviewDto>> GetVendorReviewsAsync(long vendorId, ReviewFilterDto filter);
    Task<ReviewSummaryDto> GetVendorSummaryAsync(long vendorId);
    Task<ReviewDto> CreateReviewAsync(long clientUserId, CreateReviewDto dto);
    Task<ReviewDto> RespondAsync(long vendorId, long reviewId, RespondToReviewDto dto);
}
