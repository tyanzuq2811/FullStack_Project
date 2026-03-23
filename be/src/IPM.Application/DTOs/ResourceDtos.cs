namespace IPM.Application.DTOs.Resource;

public record ResourceDto(
    int Id,
    string Name,
    string? Description,
    decimal? HourlyRate,
    bool IsActive,
    int BookingsCount
);

public record CreateResourceRequest(
    string Name,
    string? Description,
    decimal? HourlyRate
);

public record UpdateResourceRequest(
    string? Name,
    string? Description,
    decimal? HourlyRate,
    bool? IsActive
);
