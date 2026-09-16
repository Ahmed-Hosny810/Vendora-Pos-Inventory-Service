

namespace Pos.InventoryService.Application.Exceptions
{
    public class DuplicateStockWriteException : Exception
    {
        public DuplicateStockWriteException(Exception innerException)
            : base("A duplicate stock write was detected.", innerException)
        {
        }
    }
}
