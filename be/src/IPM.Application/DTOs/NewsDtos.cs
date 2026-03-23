namespace IPM.Application.DTOs.News;

public record NewsDto(
    int Id,
    string Title,
    string Content,
    bool IsPinned,
    DateTime CreatedDate
);

public record CreateNewsRequest(
    string Title,
    string Content,
    bool IsPinned = false
);

public record UpdateNewsRequest(
    string? Title,
    string? Content,
    bool? IsPinned
);
