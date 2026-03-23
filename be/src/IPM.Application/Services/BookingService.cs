using IPM.Application.DTOs.Booking;
using IPM.Application.DTOs.Common;
using IPM.Application.Services.Interfaces;
using IPM.Application.Contracts.SignalR;
using IPM.Domain.Entities;
using IPM.Domain.Enums;
using IPM.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IPM.Application.Services;

public class BookingService : IBookingService
{
    private readonly IRepository<Resource> _resourceRepository;
    private readonly IRepository<Booking> _bookingRepository;
    private readonly IRepository<Member> _memberRepository;
    private readonly IRepository<Project> _projectRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly INotificationService _notificationService;

    public BookingService(
        IRepository<Resource> resourceRepository,
        IRepository<Booking> bookingRepository,
        IRepository<Member> memberRepository,
        IRepository<Project> projectRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        INotificationService notificationService)
    {
        _resourceRepository = resourceRepository;
        _bookingRepository = bookingRepository;
        _memberRepository = memberRepository;
        _projectRepository = projectRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _notificationService = notificationService;
    }

    public async Task<ApiResponse<List<ResourceDto>>> GetResourcesAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = "resources:active";
        var cached = await _cacheService.GetAsync<List<ResourceDto>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return ApiResponse<List<ResourceDto>>.SuccessResponse(cached);
        }

        var resources = await _resourceRepository.Query()
            .Where(r => r.IsActive)
            .Select(r => new ResourceDto(r.Id, r.Name, r.Description, r.HourlyRate, r.IsActive))
            .ToListAsync(cancellationToken);

        await _cacheService.SetAsync(cacheKey, resources, TimeSpan.FromMinutes(30), cancellationToken);

        return ApiResponse<List<ResourceDto>>.SuccessResponse(resources);
    }

    public async Task<ApiResponse<List<TimeSlotDto>>> GetAvailabilityAsync(AvailabilityRequest request, CancellationToken cancellationToken = default)
    {
        var startOfDay = request.Date.Date;
        var endOfDay = startOfDay.AddDays(1);

        var existingBookings = await _bookingRepository.Query()
            .Where(b => b.ResourceId == request.ResourceId &&
                       b.StartTime >= startOfDay &&
                       b.StartTime < endOfDay &&
                       b.Status != BookingStatus.Cancelled)
            .OrderBy(b => b.StartTime)
            .ToListAsync(cancellationToken);

        // Generate hourly slots from 7:00 to 21:00
        var slots = new List<TimeSlotDto>();
        for (var hour = 7; hour < 21; hour++)
        {
            var slotStart = startOfDay.AddHours(hour);
            var slotEnd = slotStart.AddHours(1);

            var booking = existingBookings.FirstOrDefault(b =>
                (slotStart >= b.StartTime && slotStart < b.EndTime) ||
                (slotEnd > b.StartTime && slotEnd <= b.EndTime) ||
                (slotStart <= b.StartTime && slotEnd >= b.EndTime));

            slots.Add(new TimeSlotDto(slotStart, slotEnd, booking == null, booking?.Id));
        }

        return ApiResponse<List<TimeSlotDto>>.SuccessResponse(slots);
    }

    public async Task<ApiResponse<BookingDto>> CreateBookingAsync(Guid memberId, CreateBookingRequest request, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByIdAsync(memberId, cancellationToken);
        if (member == null)
        {
            return ApiResponse<BookingDto>.FailResponse("Thành viên không tồn tại");
        }

        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);
        if (project == null)
        {
            return ApiResponse<BookingDto>.FailResponse("Dự án không tồn tại");
        }

        var resource = await _resourceRepository.GetByIdAsync(request.ResourceId, cancellationToken);
        if (resource == null || !resource.IsActive)
        {
            return ApiResponse<BookingDto>.FailResponse("Tài nguyên không tồn tại hoặc đã ngừng hoạt động");
        }

        // Check for conflicts
        var hasConflict = await _bookingRepository.AnyAsync(b =>
            b.ResourceId == request.ResourceId &&
            b.Status != BookingStatus.Cancelled &&
            ((request.StartTime >= b.StartTime && request.StartTime < b.EndTime) ||
             (request.EndTime > b.StartTime && request.EndTime <= b.EndTime) ||
             (request.StartTime <= b.StartTime && request.EndTime >= b.EndTime)),
            cancellationToken);

        if (hasConflict)
        {
            return ApiResponse<BookingDto>.FailResponse("Khung giờ đã bị đặt bởi người khác");
        }

        var duration = (request.EndTime - request.StartTime).TotalHours;
        var price = resource.HourlyRate.HasValue ? resource.HourlyRate.Value * (decimal)duration : 0;

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            ResourceId = request.ResourceId,
            MemberId = memberId,
            ProjectId = request.ProjectId,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Price = price,
            Status = price > 0 ? BookingStatus.PendingPayment : BookingStatus.Confirmed
        };

        try
        {
            await _bookingRepository.AddAsync(booking, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Notify all users watching booking updates
            await _notificationService.SendBookingStatusChangedToGroupAsync("booking_updates",
                new BookingStatusChangedDto(
                    booking.Id,
                    booking.ResourceId,
                    booking.StartTime,
                    booking.EndTime,
                    (int)booking.Status
                ));

            return ApiResponse<BookingDto>.SuccessResponse(new BookingDto(
                booking.Id,
                booking.ResourceId,
                resource.Name,
                booking.MemberId,
                member.FullName,
                booking.ProjectId,
                booking.StartTime,
                booking.EndTime,
                booking.Price,
                booking.Status,
                booking.GroupId,
                booking.CreatedAt
            ), "Đặt lịch thành công");
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResponse<BookingDto>.FailResponse("Khung giờ đã bị đặt bởi người khác, vui lòng chọn khung giờ khác");
        }
        catch (DbUpdateException)
        {
            return ApiResponse<BookingDto>.FailResponse("Khung giờ đã bị đặt bởi người khác, vui lòng chọn khung giờ khác");
        }
    }

    public async Task<ApiResponse<RecurringBookingResult>> CreateRecurringBookingAsync(Guid memberId, RecurringBookingRequest request, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByIdAsync(memberId, cancellationToken);
        if (member == null)
        {
            return ApiResponse<RecurringBookingResult>.FailResponse("Thành viên không tồn tại");
        }

        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);
        if (project == null)
        {
            return ApiResponse<RecurringBookingResult>.FailResponse("Dự án không tồn tại");
        }

        var resource = await _resourceRepository.GetByIdAsync(request.ResourceId, cancellationToken);
        if (resource == null || !resource.IsActive)
        {
            return ApiResponse<RecurringBookingResult>.FailResponse("Tài nguyên không tồn tại");
        }

        var groupId = Guid.NewGuid().ToString();
        var successfulBookings = new List<BookingDto>();
        var conflicts = new List<BookingConflict>();
        var plannedSlots = new List<(DateTime date, DateTime startTime, DateTime endTime)>();

        var currentDate = request.StartDate.Date;
        while (currentDate <= request.EndDate.Date)
        {
            if (request.DaysOfWeek.Contains(currentDate.DayOfWeek))
            {
                var startTime = currentDate.Add(request.StartTime);
                var endTime = startTime.Add(request.Duration);
                plannedSlots.Add((currentDate, startTime, endTime));
            }
            currentDate = currentDate.AddDays(1);
        }

        foreach (var slot in plannedSlots)
        {
            var hasConflict = await _bookingRepository.AnyAsync(b =>
                b.ResourceId == request.ResourceId &&
                b.Status != BookingStatus.Cancelled &&
                ((slot.startTime >= b.StartTime && slot.startTime < b.EndTime) ||
                 (slot.endTime > b.StartTime && slot.endTime <= b.EndTime) ||
                 (slot.startTime <= b.StartTime && slot.endTime >= b.EndTime)),
                cancellationToken);

            if (hasConflict)
            {
                conflicts.Add(new BookingConflict(slot.date, slot.startTime, slot.endTime, "Khung giờ đã bị đặt"));
            }
        }

        if (conflicts.Any() && request.ConflictMode == BookingConflictMode.CancelEntireSeries)
        {
            return ApiResponse<RecurringBookingResult>.FailResponse(
                $"Chuỗi đặt lịch đã bị hủy vì có {conflicts.Count} ngày xung đột");
        }

        var pricePerSlot = resource.HourlyRate.HasValue
            ? resource.HourlyRate.Value * (decimal)request.Duration.TotalHours
            : 0;

        var conflictStarts = new HashSet<DateTime>(conflicts.Select(c => c.StartTime));
        foreach (var slot in plannedSlots)
        {
            if (conflictStarts.Contains(slot.startTime))
            {
                continue;
            }

            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                ResourceId = request.ResourceId,
                MemberId = memberId,
                ProjectId = request.ProjectId,
                StartTime = slot.startTime,
                EndTime = slot.endTime,
                Price = pricePerSlot,
                Status = pricePerSlot > 0 ? BookingStatus.PendingPayment : BookingStatus.Confirmed,
                GroupId = groupId
            };

            try
            {
                await _bookingRepository.AddAsync(booking, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                successfulBookings.Add(new BookingDto(
                    booking.Id,
                    booking.ResourceId,
                    resource.Name,
                    booking.MemberId,
                    member.FullName,
                    booking.ProjectId,
                    booking.StartTime,
                    booking.EndTime,
                    booking.Price,
                    booking.Status,
                    booking.GroupId,
                    booking.CreatedAt
                ));
            }
            catch (DbUpdateConcurrencyException)
            {
                conflicts.Add(new BookingConflict(slot.date, slot.startTime, slot.endTime, "Xung đột dữ liệu"));
            }
            catch (DbUpdateException)
            {
                conflicts.Add(new BookingConflict(slot.date, slot.startTime, slot.endTime, "Khung giờ đã bị đặt"));
            }
        }

        // Notify about new bookings
        if (successfulBookings.Any())
        {
            await _notificationService.SendNotificationToGroupAsync("booking_updates",
                new NotificationDto(
                    "Đặt lịch định kỳ",
                    $"Đã đặt thành công {successfulBookings.Count} lịch cho {resource.Name}",
                    "success",
                    DateTime.UtcNow
                ));
        }

        return ApiResponse<RecurringBookingResult>.SuccessResponse(
            new RecurringBookingResult(successfulBookings, conflicts),
            $"Đặt lịch thành công {successfulBookings.Count}/{successfulBookings.Count + conflicts.Count} ngày"
        );
    }

    public async Task<ApiResponse<bool>> CancelBookingAsync(Guid memberId, CancelBookingRequest request, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking == null)
        {
            return ApiResponse<bool>.FailResponse("Lịch đặt không tồn tại");
        }

        if (booking.MemberId != memberId)
        {
            return ApiResponse<bool>.FailResponse("Bạn không có quyền hủy lịch này");
        }

        if (booking.Status == BookingStatus.InUse)
        {
            return ApiResponse<bool>.FailResponse("Không thể hủy lịch đang sử dụng");
        }

        if (request.CancelEntireGroup && !string.IsNullOrEmpty(booking.GroupId))
        {
            var groupBookings = await _bookingRepository.FindAsync(
                b => b.GroupId == booking.GroupId && b.Status != BookingStatus.Cancelled,
                cancellationToken);

            foreach (var b in groupBookings)
            {
                b.Status = BookingStatus.Cancelled;
                _bookingRepository.Update(b);
            }
        }
        else
        {
            booking.Status = BookingStatus.Cancelled;
            _bookingRepository.Update(booking);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify about cancellation
        await _notificationService.SendBookingStatusChangedToGroupAsync("booking_updates",
            new BookingStatusChangedDto(
                booking.Id,
                booking.ResourceId,
                booking.StartTime,
                booking.EndTime,
                (int)BookingStatus.Cancelled
            ));

        return ApiResponse<bool>.SuccessResponse(true, "Hủy lịch thành công");
    }

    public async Task<ApiResponse<List<BookingDto>>> GetMyBookingsAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var bookings = await _bookingRepository.Query()
            .Include(b => b.Resource)
            .Include(b => b.Member)
            .Where(b => b.MemberId == memberId && b.Status != BookingStatus.Cancelled)
            .OrderBy(b => b.StartTime)
            .Select(b => new BookingDto(
                b.Id,
                b.ResourceId,
                b.Resource.Name,
                b.MemberId,
                b.Member.FullName,
                b.ProjectId,
                b.StartTime,
                b.EndTime,
                b.Price,
                b.Status,
                b.GroupId,
                b.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return ApiResponse<List<BookingDto>>.SuccessResponse(bookings);
    }

    public async Task<ApiResponse<BookingDto>> ConfirmBookingPaymentAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.Query()
            .Include(b => b.Resource)
            .Include(b => b.Member)
            .Include(b => b.Project)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
        {
            return ApiResponse<BookingDto>.FailResponse("Lịch đặt không tồn tại");
        }

        if (booking.Status != BookingStatus.PendingPayment)
        {
            return ApiResponse<BookingDto>.FailResponse("Lịch đặt không ở trạng thái chờ thanh toán");
        }

        var project = booking.Project;
        if (project.WalletBalance < booking.Price)
        {
            return ApiResponse<BookingDto>.FailResponse("Ngân sách dự án không đủ");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            project.WalletBalance -= booking.Price;
            _projectRepository.Update(project);

            booking.Status = BookingStatus.Confirmed;
            _bookingRepository.Update(booking);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            await _notificationService.SendBookingStatusChangedToGroupAsync("booking_updates",
                new BookingStatusChangedDto(
                    booking.Id,
                    booking.ResourceId,
                    booking.StartTime,
                    booking.EndTime,
                    (int)BookingStatus.Confirmed
                ));

            return ApiResponse<BookingDto>.SuccessResponse(new BookingDto(
                booking.Id,
                booking.ResourceId,
                booking.Resource.Name,
                booking.MemberId,
                booking.Member.FullName,
                booking.ProjectId,
                booking.StartTime,
                booking.EndTime,
                booking.Price,
                booking.Status,
                booking.GroupId,
                booking.CreatedAt
            ), "Thanh toán và xác nhận đặt lịch thành công");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
