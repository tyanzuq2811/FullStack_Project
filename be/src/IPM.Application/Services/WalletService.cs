using System.Security.Cryptography;
using System.Text;
using IPM.Application.DTOs.Common;
using IPM.Application.DTOs.Wallet;
using IPM.Application.Services.Interfaces;
using IPM.Application.Contracts.SignalR;
using IPM.Domain.Entities;
using IPM.Domain.Enums;
using IPM.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IPM.Application.Services;

public class WalletService : IWalletService
{
    private readonly IRepository<Member> _memberRepository;
    private readonly IRepository<Project> _projectRepository;
    private readonly IRepository<WalletTransaction> _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;

    public WalletService(
        IRepository<Member> memberRepository,
        IRepository<Project> projectRepository,
        IRepository<WalletTransaction> transactionRepository,
        IUnitOfWork unitOfWork,
        INotificationService notificationService)
    {
        _memberRepository = memberRepository;
        _projectRepository = projectRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    public async Task<ApiResponse<WalletSummaryDto>> GetWalletSummaryAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByIdAsync(memberId, cancellationToken);
        if (member == null)
        {
            return ApiResponse<WalletSummaryDto>.FailResponse("Thành viên không tồn tại");
        }

        var transactions = await _transactionRepository.FindAsync(
            t => t.MemberId == memberId && t.Status == TransactionStatus.Success,
            cancellationToken);

        var totalDeposits = transactions.Where(t => t.TransType == TransactionType.Credit).Sum(t => t.Amount);
        var totalWithdrawals = transactions.Where(t => t.TransType == TransactionType.Debit).Sum(t => t.Amount);

        return ApiResponse<WalletSummaryDto>.SuccessResponse(new WalletSummaryDto(
            member.Id,
            member.FullName,
            member.WalletBalance,
            totalDeposits,
            totalWithdrawals
        ));
    }

    public async Task<ApiResponse<PagedResult<TransactionDto>>> GetTransactionHistoryAsync(Guid memberId, WalletHistoryRequest request, CancellationToken cancellationToken = default)
    {
        var query = _transactionRepository.Query()
            .Where(t => t.MemberId == memberId);

        if (request.Category.HasValue)
            query = query.Where(t => t.Category == request.Category.Value);

        if (request.Status.HasValue)
            query = query.Where(t => t.Status == request.Status.Value);

        if (request.FromDate.HasValue)
            query = query.Where(t => t.CreatedAt >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            query = query.Where(t => t.CreatedAt <= request.ToDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TransactionDto(
                t.Id,
                t.MemberId,
                t.Category,
                t.Amount,
                t.TransType,
                t.RefId,
                t.Status,
                t.Description,
                t.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return ApiResponse<PagedResult<TransactionDto>>.SuccessResponse(new PagedResult<TransactionDto>(
            items,
            totalCount,
            request.Page,
            request.PageSize,
            (int)Math.Ceiling((double)totalCount / request.PageSize)
        ));
    }

    public async Task<ApiResponse<TransactionDto>> RequestDepositAsync(Guid memberId, DepositRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            return ApiResponse<TransactionDto>.FailResponse("Số tiền nạp phải lớn hơn 0");
        }

        if (request.ProjectId == Guid.Empty)
        {
            return ApiResponse<TransactionDto>.FailResponse("Dự án nhận phân bổ không hợp lệ");
        }

        if (string.IsNullOrWhiteSpace(request.ReceiptImageUrl))
        {
            return ApiResponse<TransactionDto>.FailResponse("Vui lòng cung cấp thông tin biên lai");
        }

        var member = await _memberRepository.GetByIdAsync(memberId, cancellationToken);
        if (member == null)
        {
            return ApiResponse<TransactionDto>.FailResponse("Thành viên không tồn tại");
        }

        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);
        if (project == null || project.ClientId != memberId)
        {
            return ApiResponse<TransactionDto>.FailResponse("Dự án không hợp lệ cho yêu cầu nạp tiền này");
        }

        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            ProjectId = request.ProjectId,
            Category = TransactionCategory.Deposit,
            Amount = request.Amount,
            TransType = TransactionType.Credit,
            Status = TransactionStatus.Pending,
            Description = request.Description ?? $"Yêu cầu nạp tiền cho dự án {project.Name}",
            RefId = BuildReceiptReference(request.ReceiptImageUrl)
        };

        try
        {
            await _transactionRepository.AddAsync(transaction, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ApiResponse<TransactionDto>.FailResponse("Không thể tạo yêu cầu nạp tiền. Vui lòng kiểm tra lại thông tin biên lai");
        }

        return ApiResponse<TransactionDto>.SuccessResponse(new TransactionDto(
            transaction.Id,
            transaction.MemberId,
            transaction.Category,
            transaction.Amount,
            transaction.TransType,
            transaction.RefId,
            transaction.Status,
            transaction.Description,
            transaction.CreatedAt
        ), "Yêu cầu nạp tiền đã được gửi, đang chờ phê duyệt");
    }

    public async Task<ApiResponse<TransactionDto>> ApproveDepositAsync(Guid adminId, ApproveDepositRequest request, CancellationToken cancellationToken = default)
    {
        var transaction = await _transactionRepository.GetByIdAsync(request.TransactionId, cancellationToken);
        if (transaction == null || transaction.Status != TransactionStatus.Pending)
        {
            return ApiResponse<TransactionDto>.FailResponse("Giao dịch không tồn tại hoặc đã được xử lý");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (request.Approved)
            {
                // Update member wallet balance
                var member = await _memberRepository.GetByIdAsync(transaction.MemberId, cancellationToken);
                if (member == null)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return ApiResponse<TransactionDto>.FailResponse("Thành viên không tồn tại");
                }

                if (!transaction.ProjectId.HasValue)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return ApiResponse<TransactionDto>.FailResponse("Giao dịch thiếu thông tin dự án để phân bổ ngân sách");
                }

                var project = await _projectRepository.GetByIdAsync(transaction.ProjectId.Value, cancellationToken);
                if (project == null || project.ClientId != transaction.MemberId)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return ApiResponse<TransactionDto>.FailResponse("Không tìm thấy dự án hợp lệ để phân bổ ngân sách");
                }

                project.WalletBalance += transaction.Amount;
                _projectRepository.Update(project);

                transaction.Status = TransactionStatus.Success;
                transaction.Description = $"{transaction.Description} - Đã phê duyệt và phân bổ vào ví dự án bởi Kế toán";

                var allocationTransaction = new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    MemberId = transaction.MemberId,
                    ProjectId = project.Id,
                    Category = TransactionCategory.Deposit,
                    Amount = transaction.Amount,
                    TransType = TransactionType.Debit,
                    Status = TransactionStatus.Success,
                    Description = $"Phân bổ vào ngân sách dự án {project.Name}",
                    RefId = transaction.Id.ToString()
                };
                allocationTransaction.EncryptedSignature = GenerateTransactionSignature(allocationTransaction);
                await _transactionRepository.AddAsync(allocationTransaction, cancellationToken);

                // Generate SHA256 signature for anti-fraud
                transaction.EncryptedSignature = GenerateTransactionSignature(transaction);

                // Send real-time notification
                await _notificationService.SendNotificationToGroupAsync($"user_{transaction.MemberId}",
                    new NotificationDto(
                        "Nạp tiền thành công",
                        $"Số tiền {transaction.Amount:N0} VNĐ đã được nạp và phân bổ vào ngân sách dự án {project.Name}",
                        "success",
                        DateTime.UtcNow
                    ));
            }
            else
            {
                transaction.Status = TransactionStatus.Failed;
                transaction.Description = $"{transaction.Description} - Bị từ chối: {request.Notes}";
            }

            _transactionRepository.Update(transaction);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return ApiResponse<TransactionDto>.SuccessResponse(new TransactionDto(
                transaction.Id,
                transaction.MemberId,
                transaction.Category,
                transaction.Amount,
                transaction.TransType,
                transaction.RefId,
                transaction.Status,
                transaction.Description,
                transaction.CreatedAt
            ), request.Approved ? "Phê duyệt thành công" : "Đã từ chối yêu cầu");
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ApiResponse<TransactionDto>> TransferAsync(Guid fromMemberId, TransferRequest request, CancellationToken cancellationToken = default)
    {
        var fromMember = await _memberRepository.GetByIdAsync(fromMemberId, cancellationToken);
        var toMember = await _memberRepository.GetByIdAsync(request.ToMemberId, cancellationToken);

        if (fromMember == null || toMember == null)
        {
            return ApiResponse<TransactionDto>.FailResponse("Người gửi hoặc người nhận không tồn tại");
        }

        if (fromMember.WalletBalance < request.Amount)
        {
            return ApiResponse<TransactionDto>.FailResponse("Số dư không đủ");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Debit from sender
            fromMember.WalletBalance -= request.Amount;
            _memberRepository.Update(fromMember);

            var debitTransaction = new WalletTransaction
            {
                Id = Guid.NewGuid(),
                MemberId = fromMemberId,
                Category = TransactionCategory.SubcontractorPayment,
                Amount = request.Amount,
                TransType = TransactionType.Debit,
                Status = TransactionStatus.Success,
                Description = request.Description ?? $"Chuyển tiền cho {toMember.FullName}",
                RefId = request.ToMemberId.ToString()
            };
            debitTransaction.EncryptedSignature = GenerateTransactionSignature(debitTransaction);
            await _transactionRepository.AddAsync(debitTransaction, cancellationToken);

            // Credit to receiver
            toMember.WalletBalance += request.Amount;
            _memberRepository.Update(toMember);

            var creditTransaction = new WalletTransaction
            {
                Id = Guid.NewGuid(),
                MemberId = request.ToMemberId,
                Category = TransactionCategory.SubcontractorPayment,
                Amount = request.Amount,
                TransType = TransactionType.Credit,
                Status = TransactionStatus.Success,
                Description = $"Nhận tiền từ {fromMember.FullName}",
                RefId = fromMemberId.ToString()
            };
            creditTransaction.EncryptedSignature = GenerateTransactionSignature(creditTransaction);
            await _transactionRepository.AddAsync(creditTransaction, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            // Notify both parties
            await _notificationService.SendWalletBalanceChangedToGroupAsync($"user_{fromMemberId}",
                new WalletBalanceChangedDto(fromMember.Id, fromMember.WalletBalance, -request.Amount, "Debit"));

            await _notificationService.SendWalletBalanceChangedToGroupAsync($"user_{request.ToMemberId}",
                new WalletBalanceChangedDto(toMember.Id, toMember.WalletBalance, request.Amount, "Credit"));

            return ApiResponse<TransactionDto>.SuccessResponse(new TransactionDto(
                debitTransaction.Id,
                debitTransaction.MemberId,
                debitTransaction.Category,
                debitTransaction.Amount,
                debitTransaction.TransType,
                debitTransaction.RefId,
                debitTransaction.Status,
                debitTransaction.Description,
                debitTransaction.CreatedAt
            ), "Chuyển tiền thành công");
        }
        catch (DbUpdateConcurrencyException)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return ApiResponse<TransactionDto>.FailResponse("Có xung đột dữ liệu, vui lòng thử lại");
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ApiResponse<List<TransactionDto>>> GetPendingDepositsAsync(CancellationToken cancellationToken = default)
    {
        var pendingDeposits = await _transactionRepository.Query()
            .Where(t => t.Status == TransactionStatus.Pending && t.Category == TransactionCategory.Deposit)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new TransactionDto(
                t.Id,
                t.MemberId,
                t.Category,
                t.Amount,
                t.TransType,
                t.RefId,
                t.Status,
                t.Description,
                t.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return ApiResponse<List<TransactionDto>>.SuccessResponse(pendingDeposits);
    }

    private static string GenerateTransactionSignature(WalletTransaction transaction)
    {
        var data = $"{transaction.Id}|{transaction.MemberId}|{transaction.Amount}|{transaction.TransType}|{transaction.CreatedAt:O}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(bytes);
    }

    private static string BuildReceiptReference(string receiptValue)
    {
        var trimmed = receiptValue.Trim();
        if (trimmed.Length <= 100)
        {
            return trimmed;
        }

        // Keep a deterministic short reference that always fits nvarchar(100).
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trimmed)))[..16];
        return $"receipt:{hash}";
    }
}
