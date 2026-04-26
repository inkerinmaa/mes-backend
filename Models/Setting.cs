namespace MyDashboardApi.Models;

public class Setting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public string? PreviousValue { get; set; }
    public int? ChangedById { get; set; }
    public string? ChangedAt { get; set; }
}
