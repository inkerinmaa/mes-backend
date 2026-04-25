namespace MyDashboardApi.Models;

public class LogEntry
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public string Level { get; set; } = "";
    public string Ts { get; set; } = "";
}
