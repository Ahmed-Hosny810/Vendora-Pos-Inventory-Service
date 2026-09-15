using AutoMapper;
using Pos.InventoryService.Application.Features.StockBalance.DTOs;
using Pos.InventoryService.Domain.Models;


namespace Pos.InventoryService.Application.Mapping
{
    public class GeneralProfile:Profile
    {
        public GeneralProfile()
        {
            CreateMap<StockBalance,StockBalanceDto>();
        }
    }
}
