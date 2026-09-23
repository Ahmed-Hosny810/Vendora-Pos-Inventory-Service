
namespace Pos.InventoryService.Infrastructure.Shared.Options
{
    public class RabbitMqOptions
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string VirtualHost { get; set; } = "pos-development";
        public string ExchangeName { get; set; } = "inventory.events";
    }
}
