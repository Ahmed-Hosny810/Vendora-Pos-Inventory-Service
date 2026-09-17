using AutoMapper;
using Pos.InventoryService.Application.Features.StockBalances.DTOs;
using Pos.InventoryService.Application.Features.StockMovements.DTOS;
using Pos.InventoryService.Domain.Models;


namespace Pos.InventoryService.Application.Mapping
{
    public class GeneralProfile:Profile
    {
        public GeneralProfile()
        {
            CreateMap<StockBalance,StockBalanceDto>();
            CreateMap<StockMovement, StockMovementDto>();
        }
    }
}
