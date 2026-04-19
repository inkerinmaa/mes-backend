namespace MyDashboardApi.Models;

public record Order(int Id, string OrderNumber, string Sku, string Priority, int Quantity, int Line, string DueDate, string Sequence);

public record Sku(int Id, string Code, string Name, string Unit);

public record CreateOrderRequest(string OrderNumber, string SkuCode, int LineId, int QuantityPackages, string Priority, string? DueDate);

public record DbUser(int Id, string Username, string FullName, string Email, string Role, string LastLogin);

public record UpdateRoleRequest(string Role);
