using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Models;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services;

public class ReviewService : IReviewService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAttachmentService _attachmentService;

    public ReviewService(IUnitOfWork unitOfWork, IAttachmentService attachmentService)
    {
        _unitOfWork = unitOfWork;
        _attachmentService = attachmentService;
    }

    public async Task<PagedResult<ReviewDto>> GetVendorReviewsAsync(long vendorId, ReviewFilterDto filter)
    {
        await EnsureVendorExistsAsync(vendorId);
        return BuildReviewsPage(vendorId, filter);
    }

    public async Task<ReviewSummaryDto> GetVendorSummaryAsync(long vendorId)
    {
        await EnsureVendorExistsAsync(vendorId);
        return BuildSummary(vendorId);
    }

    public async Task<ReviewDto> CreateReviewAsync(long clientUserId, CreateReviewDto dto)
    {
        if (dto.Rating is < 1 or > 5)
        {
            throw new BadRequestExeption("Rating must be between 1 and 5.");
        }

        var clientId = await ResolveClientIdAsync(clientUserId);

        var booking = await _unitOfWork.Repository<BookingRequest, long>().GetAsync(dto.BookingRequestId);
        if (booking is null || booking.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(BookingRequest), dto.BookingRequestId);
        }

        if (booking.Status != BookingStatus.Completed)
        {
            throw new BadRequestExeption("You can only review a booking that has been completed.");
        }

        var reviewRepo = _unitOfWork.Repository<Review, long>();
        var alreadyReviewed = reviewRepo.GetQueryable().Any(r => r.BookingRequestId == dto.BookingRequestId);
        if (alreadyReviewed)
        {
            throw new BadRequestExeption("This booking has already been reviewed.");
        }

        var review = new Review
        {
            BookingRequestId = dto.BookingRequestId,
            ClientId = clientId,
            VendorId = booking.VendorId,
            Rating = (short)dto.Rating,
            Comment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim()
        };

        await reviewRepo.AddAsync(review);
        await _unitOfWork.SaveChangesAsync();

        await RecomputeVendorRatingAsync(booking.VendorId);

        return GetReviewById(review.Id);
    }

    public async Task<ReviewDto> UpdateReviewAsync(long clientUserId, long reviewId, UpdateReviewDto dto)
    {
        if (dto.Rating is < 1 or > 5)
        {
            throw new BadRequestExeption("Rating must be between 1 and 5.");
        }

        var clientId = await ResolveClientIdAsync(clientUserId);

        var reviewRepo = _unitOfWork.Repository<Review, long>();
        var review = await reviewRepo.GetAsync(reviewId);
        if (review is null || review.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(Review), reviewId);
        }

        review.Rating = (short)dto.Rating;
        review.Comment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim();
        review.UpdatedAt = DateTimeOffset.UtcNow;
        reviewRepo.Update(review);
        await _unitOfWork.SaveChangesAsync();

        await RecomputeVendorRatingAsync(review.VendorId);

        return GetReviewById(review.Id);
    }

    public async Task<ReviewDto> RespondAsync(long vendorId, long reviewId, RespondToReviewDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Response))
        {
            throw new BadRequestExeption("A response is required.");
        }

        var reviewRepo = _unitOfWork.Repository<Review, long>();
        var review = await reviewRepo.GetWithSpecAsync(new ReviewByIdWithResponseSpecification(reviewId));
        if (review is null || review.VendorId != vendorId)
        {
            throw new NotFoundExeption(nameof(Review), reviewId);
        }

        var responseRepo = _unitOfWork.Repository<ReviewResponse, long>();
        if (review.Response is null)
        {
            var response = new ReviewResponse
            {
                ReviewId = review.Id,
                VendorId = vendorId,
                Response = dto.Response.Trim()
            };
            await responseRepo.AddAsync(response);
        }
        else
        {
            review.Response.Response = dto.Response.Trim();
            review.Response.UpdatedAt = DateTimeOffset.UtcNow;
            responseRepo.Update(review.Response);
        }

        await _unitOfWork.SaveChangesAsync();

        return GetReviewById(review.Id);
    }

    private PagedResult<ReviewDto> BuildReviewsPage(long vendorId, ReviewFilterDto filter)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 50 ? 10 : filter.PageSize;

        var query = _unitOfWork.Repository<Review, long>().GetQueryable()
            .Where(review => review.VendorId == vendorId && !review.IsHidden);

        if (filter.MinRating is not null)
        {
            query = query.Where(review => review.Rating >= filter.MinRating.Value);
        }

        var totalCount = query.Count();

        var rows = query
            .OrderByDescending(review => review.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(review => new ReviewRow
            {
                Id = review.Id,
                Rating = review.Rating,
                Comment = review.Comment,
                ClientName = review.Client.User.FullName,
                ClientAvatarUrl = review.Client.AvatarUrl,
                CreatedAt = review.CreatedAt,
                VendorResponse = review.Response != null ? review.Response.Response : null,
                VendorRespondedAt = review.Response != null ? review.Response.UpdatedAt : (DateTimeOffset?)null
            })
            .ToList();

        return new PagedResult<ReviewDto>
        {
            Items = rows.Select(MapRow).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private ReviewSummaryDto BuildSummary(long vendorId)
    {
        var ratings = _unitOfWork.Repository<Review, long>().GetQueryable()
            .Where(review => review.VendorId == vendorId && !review.IsHidden)
            .Select(review => (int)review.Rating)
            .ToList();

        var total = ratings.Count;

        return new ReviewSummaryDto
        {
            AvgRating = total == 0 ? 0m : Math.Round((decimal)ratings.Average(), 2),
            TotalReviews = total,
            FiveStar = ratings.Count(rating => rating == 5),
            FourStar = ratings.Count(rating => rating == 4),
            ThreeStar = ratings.Count(rating => rating == 3),
            TwoStar = ratings.Count(rating => rating == 2),
            OneStar = ratings.Count(rating => rating == 1)
        };
    }

    private ReviewDto GetReviewById(long reviewId)
    {
        var row = _unitOfWork.Repository<Review, long>().GetQueryable()
            .Where(review => review.Id == reviewId)
            .Select(review => new ReviewRow
            {
                Id = review.Id,
                Rating = review.Rating,
                Comment = review.Comment,
                ClientName = review.Client.User.FullName,
                ClientAvatarUrl = review.Client.AvatarUrl,
                CreatedAt = review.CreatedAt,
                VendorResponse = review.Response != null ? review.Response.Response : null,
                VendorRespondedAt = review.Response != null ? review.Response.UpdatedAt : (DateTimeOffset?)null
            })
            .FirstOrDefault();

        if (row is null)
        {
            throw new NotFoundExeption(nameof(Review), reviewId);
        }

        return MapRow(row);
    }

    private async Task RecomputeVendorRatingAsync(long vendorId)
    {
        var ratings = _unitOfWork.Repository<Review, long>().GetQueryable()
            .Where(review => review.VendorId == vendorId && !review.IsHidden)
            .Select(review => (int)review.Rating)
            .ToList();

        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(vendorId);
        if (vendor is null)
        {
            return;
        }

        vendor.TotalReviews = ratings.Count;
        vendor.AvgRating = ratings.Count == 0 ? 0m : Math.Round((decimal)ratings.Average(), 2);
        vendor.UpdatedAt = DateTimeOffset.UtcNow;
        _unitOfWork.Repository<Vendor, long>().Update(vendor);
        await _unitOfWork.SaveChangesAsync();
    }

    private ReviewDto MapRow(ReviewRow row) => new()
    {
        Id = row.Id,
        Rating = row.Rating,
        Comment = row.Comment,
        ClientName = row.ClientName,
        ClientAvatarUrl = _attachmentService.ToAbsoluteUrl(row.ClientAvatarUrl),
        CreatedAt = row.CreatedAt,
        VendorResponse = row.VendorResponse,
        VendorRespondedAt = row.VendorRespondedAt
    };

    private async Task<long> ResolveClientIdAsync(long userId)
    {
        var client = await _unitOfWork.Repository<Client, long>()
            .GetWithSpecAsync(new ClientByUserIdSpecification(userId));

        if (client is null)
        {
            throw new NotFoundExeption(nameof(Client), userId);
        }

        return client.Id;
    }

    private async Task EnsureVendorExistsAsync(long vendorId)
    {
        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(vendorId);
        if (vendor is null)
        {
            throw new NotFoundExeption(nameof(Vendor), vendorId);
        }
    }

    private sealed class ReviewRow
    {
        public long Id { get; set; }
        public short Rating { get; set; }
        public string? Comment { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string? ClientAvatarUrl { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string? VendorResponse { get; set; }
        public DateTimeOffset? VendorRespondedAt { get; set; }
    }
}
