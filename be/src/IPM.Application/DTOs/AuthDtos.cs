namespace IPM.Application.DTOs.Auth;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);

public record RegisterRequest(
    string Email,
    string Password,
    string ConfirmPassword,
    string FullName,
    string? PhoneNumber
);

public record RefreshTokenRequest(string AccessToken, string RefreshToken);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword, string ConfirmPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);

public record UserDto(
    Guid Id,
    string Email,
    string FullName,
    string? PhoneNumber,
    double RankELO,
    decimal WalletBalance,
    IList<string> Roles
);
