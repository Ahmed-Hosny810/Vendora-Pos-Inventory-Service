
namespace Pos.InventoryService.Application.Parameters
{
    public class RequestParameter<TOrderKey> where TOrderKey : Enum
    {
        private const int MaxPageSize = 50;
        private int _pageSize = 10;
        private int _pageNumber = 1;

        public int PageNumber
        {
            get => _pageNumber;
            set => _pageNumber = value < 1 ? 1 : value;
        }

        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value > MaxPageSize ? MaxPageSize : value < 1 ? 10 : value;
        }

        public bool OrderDescending { get; set; }

        public TOrderKey OrderKey { get; set; } = default!;
    }
}
