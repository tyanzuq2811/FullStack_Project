using IPM.Application.DTOs.Common;
using IPM.Application.DTOs.User;

namespace IPM.Application.Services.Interfaces;

public interface IUserService
{
    Task<ApiResponse<List<UserListDto>>> GetAllUsersAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<List<UserListDto>>> GetUsersByRoleAsync(string role, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserDto>> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserDto>> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserDto>> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteUserAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ChangeUserPasswordAsync(Guid id, ChangeUserPasswordRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ToggleUserStatusAsync(Guid id, CancellationToken cancellationToken = default);
}
