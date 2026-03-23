using IPM.Application.DTOs.Common;
using IPM.Application.DTOs.User;
using IPM.Application.Services.Interfaces;
using IPM.Domain.Entities;
using IPM.Domain.Identity;
using IPM.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IPM.Application.Services;

public class UserService : IUserService
{
    private readonly IRepository<Member> _memberRepository;
    private readonly IRepository<Project> _projectRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;

    public UserService(
        IRepository<Member> memberRepository,
        IRepository<Project> projectRepository,
        UserManager<ApplicationUser> userManager,
        IUnitOfWork unitOfWork)
    {
        _memberRepository = memberRepository;
        _projectRepository = projectRepository;
        _userManager = userManager;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<List<UserListDto>>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var members = await _memberRepository.Query()
            .OrderBy(m => m.FullName)
            .ToListAsync(cancellationToken);

        var dtos = new List<UserListDto>();
        foreach (var member in members)
        {
            var user = await _userManager.FindByIdAsync(member.UserId);
            var roles = user != null ? await _userManager.GetRolesAsync(user) : new List<string>();

            dtos.Add(new UserListDto(
                member.Id,
                member.FullName,
                member.Email,
                member.PhoneNumber,
                member.RankELO,
                member.IsActive,
                roles.ToList()
            ));
        }

        return ApiResponse<List<UserListDto>>.SuccessResponse(dtos);
    }

    public async Task<ApiResponse<List<UserListDto>>> GetUsersByRoleAsync(string role, CancellationToken cancellationToken = default)
    {
        var members = await _memberRepository.Query()
            .OrderBy(m => m.FullName)
            .ToListAsync(cancellationToken);

        var dtos = new List<UserListDto>();
        foreach (var member in members)
        {
            var user = await _userManager.FindByIdAsync(member.UserId);
            if (user == null)
            {
                continue;
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains(role))
            {
                continue;
            }

            dtos.Add(new UserListDto(
                member.Id,
                member.FullName,
                member.Email,
                member.PhoneNumber,
                member.RankELO,
                member.IsActive,
                roles.ToList()
            ));
        }

        return ApiResponse<List<UserListDto>>.SuccessResponse(dtos);
    }

    public async Task<ApiResponse<UserDto>> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByIdAsync(id, cancellationToken);
        if (member == null)
        {
            return ApiResponse<UserDto>.FailResponse("Người dùng không tồn tại");
        }

        var user = await _userManager.FindByIdAsync(member.UserId);
        var roles = user != null ? await _userManager.GetRolesAsync(user) : new List<string>();

        var dto = new UserDto(
            member.Id,
            member.UserId,
            member.FullName,
            member.Email,
            member.PhoneNumber,
            member.RankELO,
            member.WalletBalance,
            member.IsActive,
            roles.ToList()
        );

        return ApiResponse<UserDto>.SuccessResponse(dto);
    }

    public async Task<ApiResponse<UserDto>> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        // Check if email already exists
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return ApiResponse<UserDto>.FailResponse("Email đã được sử dụng");
        }

        // Validate role
        var validRoles = new[] { "Admin", "ProjectManager", "Accountant", "Subcontractor", "Client" };
        if (!validRoles.Contains(request.Role))
        {
            return ApiResponse<UserDto>.FailResponse("Vai trò không hợp lệ");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Create ApplicationUser
            var appUser = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(appUser, request.Password);
            if (!createResult.Succeeded)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return ApiResponse<UserDto>.FailResponse(string.Join(", ", createResult.Errors.Select(e => e.Description)));
            }

            // Assign role
            await _userManager.AddToRoleAsync(appUser, request.Role);

            // Create Member
            var member = new Member
            {
                Id = Guid.NewGuid(),
                UserId = appUser.Id,
                FullName = request.FullName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                RankELO = 1200,
                WalletBalance = 0,
                IsActive = true
            };

            await _memberRepository.AddAsync(member, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            var dto = new UserDto(
                member.Id,
                member.UserId,
                member.FullName,
                member.Email,
                member.PhoneNumber,
                member.RankELO,
                member.WalletBalance,
                member.IsActive,
                new List<string> { request.Role }
            );

            return ApiResponse<UserDto>.SuccessResponse(dto, "Tạo người dùng thành công");
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ApiResponse<UserDto>> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByIdAsync(id, cancellationToken);
        if (member == null)
        {
            return ApiResponse<UserDto>.FailResponse("Người dùng không tồn tại");
        }

        var user = await _userManager.FindByIdAsync(member.UserId);
        if (user == null)
        {
            return ApiResponse<UserDto>.FailResponse("Tài khoản không tồn tại");
        }

        // Update member info
        if (!string.IsNullOrEmpty(request.FullName))
            member.FullName = request.FullName;

        if (!string.IsNullOrEmpty(request.Email) && request.Email != member.Email)
        {
            var existing = await _userManager.FindByEmailAsync(request.Email);
            if (existing != null && existing.Id != user.Id)
            {
                return ApiResponse<UserDto>.FailResponse("Email đã được sử dụng");
            }
            member.Email = request.Email;
            user.Email = request.Email;
            user.UserName = request.Email;
            await _userManager.UpdateAsync(user);
        }

        if (request.PhoneNumber != null)
            member.PhoneNumber = request.PhoneNumber;

        if (request.IsActive.HasValue)
            member.IsActive = request.IsActive.Value;

        // Update role if specified
        if (!string.IsNullOrEmpty(request.Role))
        {
            var validRoles = new[] { "Admin", "ProjectManager", "Accountant", "Subcontractor", "Client" };
            if (!validRoles.Contains(request.Role))
            {
                return ApiResponse<UserDto>.FailResponse("Vai trò không hợp lệ");
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, request.Role);
        }

        _memberRepository.Update(member);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var roles = await _userManager.GetRolesAsync(user);
        var dto = new UserDto(
            member.Id,
            member.UserId,
            member.FullName,
            member.Email,
            member.PhoneNumber,
            member.RankELO,
            member.WalletBalance,
            member.IsActive,
            roles.ToList()
        );

        return ApiResponse<UserDto>.SuccessResponse(dto, "Cập nhật người dùng thành công");
    }

    public async Task<ApiResponse<bool>> DeleteUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByIdAsync(id, cancellationToken);
        if (member == null)
        {
            return ApiResponse<bool>.FailResponse("Người dùng không tồn tại");
        }

        var user = await _userManager.FindByIdAsync(member.UserId);

        var hasProjectsAsClient = await _projectRepository.Query()
            .AnyAsync(p => p.ClientId == member.Id, cancellationToken);

        if (hasProjectsAsClient)
        {
            return ApiResponse<bool>.FailResponse("Không thể xóa người dùng đang là khách hàng của dự án. Vui lòng chuyển dự án cho tài khoản khác trước.");
        }

        var hasProjectsAsManager = await _projectRepository.Query()
            .AnyAsync(p => p.ManagerId == member.Id, cancellationToken);

        if (hasProjectsAsManager)
        {
            return ApiResponse<bool>.FailResponse("Không thể xóa người dùng đang là quản lý dự án. Vui lòng chuyển PM cho dự án trước.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _memberRepository.Remove(member);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (user != null)
            {
                var deleteUserResult = await _userManager.DeleteAsync(user);
                if (!deleteUserResult.Succeeded)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return ApiResponse<bool>.FailResponse(string.Join(", ", deleteUserResult.Errors.Select(e => e.Description)));
                }
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return ApiResponse<bool>.SuccessResponse(true, "Đã xóa người dùng khỏi hệ thống");
    }

    public async Task<ApiResponse<bool>> ChangeUserPasswordAsync(Guid id, ChangeUserPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByIdAsync(id, cancellationToken);
        if (member == null)
        {
            return ApiResponse<bool>.FailResponse("Người dùng không tồn tại");
        }

        var user = await _userManager.FindByIdAsync(member.UserId);
        if (user == null)
        {
            return ApiResponse<bool>.FailResponse("Tài khoản không tồn tại");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);

        if (!result.Succeeded)
        {
            return ApiResponse<bool>.FailResponse(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return ApiResponse<bool>.SuccessResponse(true, "Đổi mật khẩu thành công");
    }

    public async Task<ApiResponse<bool>> ToggleUserStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByIdAsync(id, cancellationToken);
        if (member == null)
        {
            return ApiResponse<bool>.FailResponse("Người dùng không tồn tại");
        }

        member.IsActive = !member.IsActive;
        _memberRepository.Update(member);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var status = member.IsActive ? "kích hoạt" : "vô hiệu hóa";
        return ApiResponse<bool>.SuccessResponse(true, $"Đã {status} người dùng");
    }
}
