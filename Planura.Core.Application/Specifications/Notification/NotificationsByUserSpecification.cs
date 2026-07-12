using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications.Notification
{
    public class NotificationsByUserSpecification
        : BaseSpecification<Planura.Core.Domain.Entities.Notification>
    {
        public NotificationsByUserSpecification(long userId, bool unreadOnly = false)
            : base(n => n.UserId == userId && (!unreadOnly || !n.IsRead))
        {
            ApplyOrderByDescending(n => n.CreatedAt);
        }
    }
}
