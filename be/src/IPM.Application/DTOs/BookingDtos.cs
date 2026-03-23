using IPM.Domain.Enums;

namespace IPM.Application.DTOs.Booking;

public record ResourceDto(
    int Id,
    string Name,
    string? Description,
    decimal? HourlyRate,
    bool IsActive
);

public record BookingDto(
    Guid Id,
    int ResourceId,
    string ResourceName,
    Guid MemberId,
    string MemberName,
    Guid ProjectId,
    DateTime StartTime,
    DateTime EndTime,
    decimal Price,
    BookingStatus Status,
    string? GroupId,
    DateTime CreatedAt
);

public record CreateBookingRequest(
    int ResourceId,
    Guid ProjectId,
    DateTime StartTime,
    DateTime EndTime
);

public record RecurringBookingRequest(
    int ResourceId,
    Guid ProjectId,
    DateTime StartDate,
    DateTime EndDate,
    TimeSpan StartTime,
    TimeSpan Duration,
    List<DayOfWeek> DaysOfWeek,
    BookingConflictMode ConflictMode = BookingConflictMode.SkipConflicts
);

public record RecurringBookingResult(
    List<BookingDto> SuccessfulBookings,
    List<BookingConflict> Conflicts
);

public record BookingConflict(
    DateTime Date,
    DateTime StartTime,
    DateTime EndTime,
    string ConflictReason
);

public enum BookingConflictMode
{
    SkipConflicts = 0,
    CancelEntireSeries = 1
}

public record AvailabilityRequest(
    int ResourceId,
    DateTime Date
);

public record TimeSlotDto(
    DateTime StartTime,
    DateTime EndTime,
    bool IsAvailable,
    Guid? BookingId
);

public record CancelBookingRequest(
    Guid BookingId,
    bool CancelEntireGroup = false
);
