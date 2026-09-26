namespace Pos.InventoryService.Application.Features.StockTransfers.DTOS
{
    public class StockTransferDetailsDto : StockTransferDto
    {
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public List<StockTransferItemDetailsDto> Items { get; set; } = new();
    }

    public class StockTransferItemDetailsDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public Guid? ProductVariantId { get; set; }
        public decimal Quantity { get; set; }
        public decimal ReceivedQuantity { get; set; }
        public decimal RemainingQuantity => Quantity - ReceivedQuantity;
    }
}

