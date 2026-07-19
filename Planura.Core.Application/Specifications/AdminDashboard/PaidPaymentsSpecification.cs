using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Planura.Core.Application.Specifications.AdminDashboard;

public class PaidPaymentsSpecification : BaseSpecification<Payment>
{
    public PaidPaymentsSpecification()
        : base(p => p.Status == PaymentStatus.Completed)
    {
    }
}