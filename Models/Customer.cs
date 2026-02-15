namespace MyDashboardApi.Models;

public record Avatar(string Src, string Alt);

public record Customer(int Id, string Name, string Email, Avatar Avatar, string Status, string Location);
