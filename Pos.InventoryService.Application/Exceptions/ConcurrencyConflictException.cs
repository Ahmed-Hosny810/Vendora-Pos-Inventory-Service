
namespace Pos.InventoryService.Application.Exceptions
{
    public class ConcurrencyConflictException : Exception
    {
        public ConcurrencyConflictException(Exception innerException)
            : base("The record was changed by another request.", innerException)
        {
        }
    }
}
