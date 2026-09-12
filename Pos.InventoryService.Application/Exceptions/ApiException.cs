using System.Globalization;


namespace Pos.InventoryService.Application.Exceptions
{
    public class ApiException : Exception
    {
        public ApiException() { }

        public ApiException(string message) : base(message) { }

        public ApiException(string message, params object[] args) :
            base(String.Format(CultureInfo.CurrentCulture, message, args))
        { }

    }
}
