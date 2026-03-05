namespace MyDashboardApi.Models;

public record Order(int Id, string Sku, string Priority, int Quantity, int Line, string DueDate, string Sequence);
