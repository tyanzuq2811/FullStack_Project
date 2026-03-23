using IPM.Domain.Enums;

namespace IPM.Application.DTOs.Wallet;

public record WalletSummaryDto(
    Guid MemberId,
    string MemberName,
    decimal Balance,
    decimal TotalDeposits,
    decimal TotalWithdrawals
);

public record TransactionDto(
    Guid Id,
    Guid MemberId,
    TransactionCategory Category,
    decimal Amount,
    TransactionType TransType,
    string? RefId,
    TransactionStatus Status,
    string? Description,
    DateTime CreatedAt
);

public record DepositRequest(
    decimal Amount,
    Guid ProjectId,
    string ReceiptImageUrl,
    string? Description
);

public record ApproveDepositRequest(
    Guid TransactionId,
    bool Approved,
    string? Notes
);

public record TransferRequest(
    Guid ToMemberId,
    decimal Amount,
    string? Description
);

public record WalletHistoryRequest(
    int Page = 1,
    int PageSize = 20,
    TransactionCategory? Category = null,
    TransactionStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null
);
