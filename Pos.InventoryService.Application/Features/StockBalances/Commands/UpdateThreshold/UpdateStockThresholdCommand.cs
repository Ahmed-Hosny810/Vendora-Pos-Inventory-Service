using MediatR;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;

namespace Pos.InventoryService.Application.Features.StockBalances.Commands.UpdateThreshold;

public class UpdateStockThresholdCommand : IRequest<Result<Guid>>
{
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public decimal LowStockThreshold { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class UpdateStockThresholdCommandHandler
    : IRequestHandler<UpdateStockThresholdCommand, Result<Guid>>
{
    private readonly IStockBalanceRepositoryAsync _balances;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateStockThresholdCommandHandler(
        IStockBalanceRepositoryAsync balances,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _balances = balances;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(
        UpdateStockThresholdCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            throw new UnauthorizedAccessException("A valid tenant is required.");

        var balance = await _balances.GetProductStockBalanceAsync(
            tenantId.Value, request.BranchId,
            request.ProductId, request.ProductVariantId, cancellationToken);

        if (balance == null)
            return Result<Guid>.Failure("Stock balance was not found.");

        if (!balance.RowVersion.SequenceEqual(request.RowVersion))
            return Result<Guid>.Failure("This balance has changed. Reload it and try again.");

        balance.LowStockThreshold = request.LowStockThreshold;
        balance.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<Guid>.Failure("Stock changed while updating the threshold. Reload and retry.");
        }
        catch (DuplicateStockWriteException)
        {
            return Result<Guid>.Failure("The stock alert changed concurrently. Reload and retry.");
        }

        return Result<Guid>.Success(balance.Id);
    }
}

