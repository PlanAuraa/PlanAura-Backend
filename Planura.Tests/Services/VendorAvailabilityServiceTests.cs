using AutoMapper;
using Moq;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;
using Planura.Tests.TestHelpers;
using Xunit;

namespace Planura.Tests.Services;

public class VendorAvailabilityServiceTests
{
    private const long VendorId = 20;

    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IMapper> _mapperMock = new();

    private VendorAvailabilityService CreateService() => new(_unitOfWorkMock.Object, _mapperMock.Object);

    /// <summary>
    /// Regression test for the reported 3-hour offset bug: a vendor entering "12:00" for a recurring
    /// slot expects that to mean 12:00 Egypt local time, not 12:00 UTC. GenerateRecurringAsync used to
    /// build StartAt/EndAt with a zero (UTC) offset; it must now use Egypt's fixed +03:00 offset, so
    /// the resulting DateTimeOffset's wall-clock hour/minute still reads "12:00" while its UTC instant
    /// is correctly 3 hours earlier.
    /// </summary>
    [Fact]
    public async Task GenerateRecurringAsync_BuildsSlotsInEgyptLocalTime_NotUtc()
    {
        var vendorRepo = _unitOfWorkMock.SetupRepository<Vendor, long>();
        vendorRepo.Setup(r => r.GetAsync(VendorId)).ReturnsAsync(new Vendor { Id = VendorId });

        var slotRepo = _unitOfWorkMock.SetupRepository<VendorAvailability, long>();
        slotRepo.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<VendorAvailability>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<VendorAvailability>());

        List<VendorAvailability>? captured = null;
        slotRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<VendorAvailability>>()))
            .Callback<IEnumerable<VendorAvailability>>(slots => captured = slots.ToList())
            .Returns(Task.CompletedTask);

        var dto = new CreateRecurringAvailabilityDto
        {
            DaysOfWeek = [1], // Monday
            StartTime = new TimeOnly(12, 0),
            EndTime = new TimeOnly(13, 0),
            StartDate = new DateOnly(2026, 8, 10), // a Monday
            RepeatMonths = 1,
        };

        var service = CreateService();
        var result = await service.GenerateRecurringAsync(VendorId, dto);

        Assert.True(result.CreatedCount > 0);
        Assert.NotNull(captured);

        var first = captured![0];

        // Wall-clock time as the vendor typed it, preserved via the DateTimeOffset's own offset.
        Assert.Equal(12, first.StartAt.Hour);
        Assert.Equal(0, first.StartAt.Minute);
        Assert.Equal(TimeSpan.FromHours(3), first.StartAt.Offset);
        Assert.Equal(13, first.EndAt.Hour);
        Assert.Equal(TimeSpan.FromHours(3), first.EndAt.Offset);

        // The absolute instant is 3 hours behind the Egypt wall-clock reading, i.e. NOT stored as if
        // "12:00" meant UTC (which would put UtcDateTime.Hour at 12, not 9).
        Assert.Equal(9, first.StartAt.UtcDateTime.Hour);
    }
}
