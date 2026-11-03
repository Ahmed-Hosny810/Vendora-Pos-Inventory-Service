using MediatR;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;

namespace Pos.InventoryService.Application.Features.StockReturns.Commands.RestockCommand;

public class RestockCustomerReturnCommandHandler : IRequestHandler<RestockCustomerReturnCommand, Result<Guid>>
{
    private readonly ICustomerReturnRestockService _restockService;
    private readonly IStockMovementRepository _movements;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public RestockCustomerReturnCommandHandler(
        ICustomerReturnRestockService restockService,
        IStockMovementRepository movements,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _restockService = restockService;
        _movements = movements;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(
        RestockCustomerReturnCommand request, CancellationToken cancellationToken)
    {
        // 1. Get the authenticated tenant and user.
        var tenantId = _currentUser.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            throw new UnauthorizedAccessException("A valid tenant is required.");

        if (!Guid.TryParse(_currentUser.UserId, out var userId) || userId == Guid.Empty)
            throw new UnauthorizedAccessException("A valid user is required.");

        var result = await _restockService.StageRestockAsync(
            tenantId.Value, userId, request.ReturnId, request.BranchId,
            request.IdempotencyKey, request.Items, cancellationToken);

        if (result.IsFailure)
            return result;

        // Save balances and movements together.
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<Guid>.Failure(
                "Stock changed while restocking. Retry the same return with the same idempotency key.");
        }
        catch (DuplicateStockWriteException)
        {
            // A concurrent copy may already have completed this return.
            if (await _movements.ReturnRestockExistsAsync(
                tenantId.Value, request.ReturnId, cancellationToken))
            {
                return Result<Guid>.Success(request.ReturnId);
            }

            return Result<Guid>.Failure(
                "A stock write conflicted. Retry the same return with the same idempotency key.");
        }

        return Result<Guid>.Success(request.ReturnId);
    }
}

