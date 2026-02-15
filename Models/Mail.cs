namespace MyDashboardApi.Models;

public record MailSender(int Id, string Name, string Email, Avatar Avatar, string Status, string Location);

public record Mail(int Id, bool Unread, MailSender From, string Subject, string Body, string Date);
