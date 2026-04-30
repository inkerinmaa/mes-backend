namespace MyDashboardApi.Models;

public class ProductionEvent
{
    public int Id { get; set; }
    public int LineId { get; set; }
    public string LineName { get; set; } = "";
    public int? OrderId { get; set; }
    public int? MachineStateId { get; set; }
    public string EventType { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string StartAt { get; set; } = "";
    public string? EndAt { get; set; }
    public string? CreatedBy { get; set; }
    public string CreatedAt { get; set; } = "";
}

public class UnacknowledgedStop
{
    public int Id { get; set; }
    public int LineId { get; set; }
    public string LineName { get; set; } = "";
    public string StartAt { get; set; } = "";
    public int DurationMinutes { get; set; }
}

public record CreateEventRequest(
    int LineId,
    int? OrderId,
    int? MachineStateId,
    string EventType,
    string Severity,
    string Title,
    string? Description,
    string? StartAt,
    string? EndAt
);

public record CloseEventRequest(string? EndAt, string? Description);
