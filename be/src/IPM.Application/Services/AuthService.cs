using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Hangfire;
using IPM.Application.DTOs.Auth;
using IPM.Application.DTOs.Common;
using IPM.Application.Services.Interfaces;
using IPM.Application.Contracts.SignalR;
using IPM.Domain.Entities;
using IPM.Domain.Interfaces;
using IPM.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace IPM.Application.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRepository<Member> _memberRepository;
    private readonly IRepository<RefreshToken> _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IRepository<Member> memberRepository,
        IRepository<RefreshToken> refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _memberRepository = memberRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _configuration = configuration;
    }

    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return ApiResponse<LoginResponse>.FailResponse("Email hoặc mật khẩu không chính xác");
        }

        var member = await _memberRepository.FirstOrDefaultAsync(m => m.UserId == user.Id, cancellationToken);
        if (member == null || !member.IsActive)
        {
            return ApiResponse<LoginResponse>.FailResponse("Tài khoản đã bị vô hiệu hóa");
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var (accessToken, jwtId) = GenerateAccessToken(user, member, roles);
        var refreshToken = await GenerateRefreshTokenAsync(member.Id, jwtId, cancellationToken);

        var userDto = new UserDto(
            member.Id,
            member.Email,
            member.FullName,
            member.PhoneNumber,
            member.RankELO,
            member.WalletBalance,
            roles
        );

        return ApiResponse<LoginResponse>.SuccessResponse(new LoginResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(double.Parse(_configuration["Jwt:ExpiresInMinutes"] ?? "60")),
            userDto
        ));
    }

    public async Task<ApiResponse<LoginResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Password != request.ConfirmPassword)
        {
            return ApiResponse<LoginResponse>.FailResponse("Mật khẩu xác nhận không khớp");
        }

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return ApiResponse<LoginResponse>.FailResponse("Email đã được sử dụng");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return ApiResponse<LoginResponse>.FailResponse("Đăng ký thất bại", result.Errors.Select(e => e.Description).ToList());
        }

        // Public signup is always Client; internal roles are assigned by Admin.
        var addRoleResult = await _userManager.AddToRoleAsync(user, "Client");
        if (!addRoleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return ApiResponse<LoginResponse>.FailResponse("Đăng ký thất bại", addRoleResult.Errors.Select(e => e.Description).ToList());
        }

        // Create member profile
        var member = new Member
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Email = request.Email,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            RankELO = 1200,
            WalletBalance = 0,
            IsActive = true
        };

        await _memberRepository.AddAsync(member, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var roles = await _userManager.GetRolesAsync(user);
        var (accessToken, jwtId) = GenerateAccessToken(user, member, roles);
        var refreshToken = await GenerateRefreshTokenAsync(member.Id, jwtId, cancellationToken);

        var userDto = new UserDto(
            member.Id,
            member.Email,
            member.FullName,
            member.PhoneNumber,
            member.RankELO,
            member.WalletBalance,
            roles
        );

        return ApiResponse<LoginResponse>.SuccessResponse(new LoginResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(double.Parse(_configuration["Jwt:ExpiresInMinutes"] ?? "60")),
            userDto
        ), "Đăng ký thành công");
    }

    public async Task<ApiResponse<LoginResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var principal = GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal == null)
        {
            return ApiResponse<LoginResponse>.FailResponse("Token không hợp lệ");
        }

        var jwtId = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
        var memberIdClaim = principal.Claims.FirstOrDefault(c => c.Type == "memberId")?.Value;

        if (string.IsNullOrEmpty(jwtId) || string.IsNullOrEmpty(memberIdClaim))
        {
            return ApiResponse<LoginResponse>.FailResponse("Token không hợp lệ");
        }

        var memberId = Guid.Parse(memberIdClaim);
        var storedToken = await _refreshTokenRepository.FirstOrDefaultAsync(
            t => t.Token == request.RefreshToken && t.JwtId == jwtId && t.MemberId == memberId,
            cancellationToken);

        if (storedToken == null || storedToken.IsUsed || storedToken.IsRevoked || storedToken.ExpiryDate < DateTime.UtcNow)
        {
            return ApiResponse<LoginResponse>.FailResponse("Refresh token không hợp lệ hoặc đã hết hạn");
        }

        // Mark token as used
        storedToken.IsUsed = true;
        _refreshTokenRepository.Update(storedToken);

        var member = await _memberRepository.GetByIdAsync(memberId, cancellationToken);
        if (member == null || !member.IsActive)
        {
            return ApiResponse<LoginResponse>.FailResponse("Tài khoản không tồn tại hoặc đã bị vô hiệu hóa");
        }

        var user = await _userManager.FindByIdAsync(member.UserId);
        if (user == null)
        {
            return ApiResponse<LoginResponse>.FailResponse("Người dùng không tồn tại");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var (newAccessToken, newJwtId) = GenerateAccessToken(user, member, roles);
        var newRefreshToken = await GenerateRefreshTokenAsync(member.Id, newJwtId, cancellationToken);

        var userDto = new UserDto(
            member.Id,
            member.Email,
            member.FullName,
            member.PhoneNumber,
            member.RankELO,
            member.WalletBalance,
            roles
        );

        return ApiResponse<LoginResponse>.SuccessResponse(new LoginResponse(
            newAccessToken,
            newRefreshToken,
            DateTime.UtcNow.AddMinutes(double.Parse(_configuration["Jwt:ExpiresInMinutes"] ?? "60")),
            userDto
        ));
    }

    public async Task<ApiResponse<bool>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            // Don't reveal that user doesn't exist
            return ApiResponse<bool>.SuccessResponse(true, "Nếu email tồn tại, bạn sẽ nhận được link đặt lại mật khẩu");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetLink = $"{_configuration["App:FrontendUrl"]}/reset-password?email={request.Email}&token={Uri.EscapeDataString(token)}";

        // Send email via background job
        BackgroundJob.Enqueue<IEmailService>(s => s.SendPasswordResetAsync(request.Email, resetLink, CancellationToken.None));

        return ApiResponse<bool>.SuccessResponse(true, "Nếu email tồn tại, bạn sẽ nhận được link đặt lại mật khẩu");
    }

    public async Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            return ApiResponse<bool>.FailResponse("Mật khẩu xác nhận không khớp");
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return ApiResponse<bool>.FailResponse("Email không tồn tại");
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            return ApiResponse<bool>.FailResponse("Đặt lại mật khẩu thất bại", result.Errors.Select(e => e.Description).ToList());
        }

        // Revoke all refresh tokens
        var member = await _memberRepository.FirstOrDefaultAsync(m => m.UserId == user.Id, cancellationToken);
        if (member != null)
        {
            await RevokeTokenAsync(member.Id, cancellationToken);
        }

        return ApiResponse<bool>.SuccessResponse(true, "Đặt lại mật khẩu thành công");
    }

    public async Task<ApiResponse<bool>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            return ApiResponse<bool>.FailResponse("Mật khẩu xác nhận không khớp");
        }

        var member = await _memberRepository.GetByIdAsync(userId, cancellationToken);
        if (member == null)
        {
            return ApiResponse<bool>.FailResponse("Người dùng không tồn tại");
        }

        var user = await _userManager.FindByIdAsync(member.UserId);
        if (user == null)
        {
            return ApiResponse<bool>.FailResponse("Người dùng không tồn tại");
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return ApiResponse<bool>.FailResponse("Đổi mật khẩu thất bại", result.Errors.Select(e => e.Description).ToList());
        }

        return ApiResponse<bool>.SuccessResponse(true, "Đổi mật khẩu thành công");
    }

    public async Task<ApiResponse<UserDto>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByIdAsync(userId, cancellationToken);
        if (member == null)
        {
            return ApiResponse<UserDto>.FailResponse("Người dùng không tồn tại");
        }

        var user = await _userManager.FindByIdAsync(member.UserId);
        if (user == null)
        {
            return ApiResponse<UserDto>.FailResponse("Người dùng không tồn tại");
        }

        var roles = await _userManager.GetRolesAsync(user);

        return ApiResponse<UserDto>.SuccessResponse(new UserDto(
            member.Id,
            member.Email,
            member.FullName,
            member.PhoneNumber,
            member.RankELO,
            member.WalletBalance,
            roles
        ));
    }

    public async Task RevokeTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tokens = await _refreshTokenRepository.FindAsync(t => t.MemberId == userId && !t.IsRevoked, cancellationToken);
        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            _refreshTokenRepository.Update(token);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private (string token, string jwtId) GenerateAccessToken(ApplicationUser user, Member member, IList<string> roles)
    {
        var jwtId = Guid.NewGuid().ToString();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, jwtId),
            new("memberId", member.Id.ToString()),
            new("fullName", member.FullName)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(double.Parse(_configuration["Jwt:ExpiresInMinutes"] ?? "60"));

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), jwtId);
    }

    private async Task<string> GenerateRefreshTokenAsync(Guid memberId, string jwtId, CancellationToken cancellationToken)
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        var refreshToken = Convert.ToBase64String(randomBytes);

        var refreshTokenEntity = new RefreshToken
        {
            MemberId = memberId,
            Token = refreshToken,
            JwtId = jwtId,
            ExpiryDate = DateTime.UtcNow.AddDays(7),
            IsUsed = false,
            IsRevoked = false
        };

        await _refreshTokenRepository.AddAsync(refreshTokenEntity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return refreshToken;
    }

    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]!)),
            ValidateLifetime = false,
            ValidIssuer = _configuration["Jwt:Issuer"],
            ValidAudience = _configuration["Jwt:Audience"]
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }
            return principal;
        }
        catch
        {
            return null;
        }
    }
}
