using IPM.Application.DTOs.Common;
using IPM.Application.DTOs.Wallet;

namespace IPM.Application.Services.Interfaces;

public interface IWalletService
{
    Task<ApiResponse<WalletSummaryDto>> GetWalletSummaryAsync(Guid memberId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PagedResult<TransactionDto>>> GetTransactionHistoryAsync(Guid memberId, WalletHistoryRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<TransactionDto>> RequestDepositAsync(Guid memberId, DepositRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<TransactionDto>> ApproveDepositAsync(Guid adminId, ApproveDepositRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<TransactionDto>> TransferAsync(Guid fromMemberId, TransferRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<TransactionDto>>> GetPendingDepositsAsync(CancellationToken cancellationToken = default);
}
