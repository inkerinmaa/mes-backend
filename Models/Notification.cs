namespace MyDashboardApi.Models;

public record NotificationSender(int Id, string Name, string Email, Avatar Avatar, string Status, string Location);

public record Notification(int Id, bool Unread, NotificationSender Sender, string Body, string Date);
