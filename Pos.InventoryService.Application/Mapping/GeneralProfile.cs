using AutoMapper;
using Pos.InventoryService.Application.Features.LowStockAlerts.DTOs;
using Pos.InventoryService.Application.Features.StockAdjustments.DTOS;
using Pos.InventoryService.Application.Features.StockBalances.DTOs;
using Pos.InventoryService.Application.Features.StockMovements.DTOS;
using Pos.InventoryService.Application.Features.StockTransfers.DTOS;
using Pos.InventoryService.Domain.Models;


namespace Pos.InventoryService.Application.Mapping
{
    public class GeneralProfile:Profile
    {
        public GeneralProfile()
        {
            CreateMap<StockBalance,StockBalanceDto>();
            CreateMap<LowStockAlert, LowStockAlertDto>();
            CreateMap<StockMovement, StockMovementDto>();
            CreateMap<StockAdjustment, StockAdjustmentDetailsDto>();
            CreateMap<StockAdjustmentItem, StockAdjustmentItemDto>();
            CreateMap<StockTransfer, StockTransferDto>();
            CreateMap<StockTransfer, StockTransferDetailsDto>();
            CreateMap<StockTransferItem, StockTransferItemDto>();
        }
    }
}
