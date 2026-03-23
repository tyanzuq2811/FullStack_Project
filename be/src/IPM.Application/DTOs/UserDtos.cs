namespace IPM.Application.DTOs.User;

public record UserDto(
    Guid Id,
    string UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    double RankELO,
    decimal WalletBalance,
    bool IsActive,
    List<string> Roles
);

public record UserListDto(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    double RankELO,
    bool IsActive,
    List<string> Roles
);

public record CreateUserRequest(
    string FullName,
    string Email,
    string Password,
    string? PhoneNumber,
    string Role
);

public record UpdateUserRequest(
    string? FullName,
    string? Email,
    string? PhoneNumber,
    bool? IsActive,
    string? Role
);

public record ChangeUserPasswordRequest(
    string NewPassword
);
