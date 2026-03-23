using IPM.Application.DTOs.Booking;
using IPM.Application.DTOs.Common;

namespace IPM.Application.Services.Interfaces;

public interface IBookingService
{
    Task<ApiResponse<List<ResourceDto>>> GetResourcesAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<List<TimeSlotDto>>> GetAvailabilityAsync(AvailabilityRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<BookingDto>> CreateBookingAsync(Guid memberId, CreateBookingRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<RecurringBookingResult>> CreateRecurringBookingAsync(Guid memberId, RecurringBookingRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> CancelBookingAsync(Guid memberId, CancelBookingRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<BookingDto>>> GetMyBookingsAsync(Guid memberId, CancellationToken cancellationToken = default);
    Task<ApiResponse<BookingDto>> ConfirmBookingPaymentAsync(Guid bookingId, CancellationToken cancellationToken = default);
}
