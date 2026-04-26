namespace MyDashboardApi.Models;

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public string Sku { get; set; } = "";
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public decimal Volume { get; set; }
    public string UomCode { get; set; } = "";
    public int Line { get; set; }
    public string DueDate { get; set; } = "";
    public string? PlannedStartAt { get; set; }
    public string? PlannedCompleteAt { get; set; }
    public string? StartAt { get; set; }
    public string? CompleteAt { get; set; }
    public string Sequence { get; set; } = "";
    public bool Cage { get; set; }
    public int ProducedPackages { get; set; }
    public string? Comment { get; set; }
}

public class OrderDetail
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public string Sku { get; set; } = "";
    public string Priority { get; set; } = "";
    public decimal Volume { get; set; }
    public string UomCode { get; set; } = "";
    public string UomName { get; set; } = "";
    public int Line { get; set; }
    public string DueDate { get; set; } = "";
    public string Status { get; set; } = "";
    public string? PlannedStartAt { get; set; }
    public string? PlannedCompleteAt { get; set; }
    public string? StartAt { get; set; }
    public string? CompleteAt { get; set; }
    public bool Cage { get; set; }
    public int CageSize { get; set; }
    public string? Comment { get; set; }
    public int ProducedPackages { get; set; }
    public List<CageEntry> Cages { get; set; } = [];
}

public class CageEntry
{
    public int Id { get; set; }
    public string CageGuid { get; set; } = "";
    public int CageSize { get; set; }
    public int Packages { get; set; }
    public string ScannedAt { get; set; } = "";
    public string? ScannedBy { get; set; }
}

public class Uom
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
}

public record Sku(int Id, string Code, string Name, string Unit);

public record CreateOrderRequest(string OrderNumber, string SkuCode, int LineId, decimal Volume, string UomCode, string Priority, string? DueDate, string? PlannedStartAt, string? PlannedCompleteAt, bool Cage, int CageSize = 50);
public record TransitionStatusRequest(string Action); // action: "start" | "pause" | "complete"

public record ScanCageRequest(string QrData);

public record UpdateCagePackagesRequest(int Packages);

public record UpdateCageSizeRequest(int CageSize);

public record UpdateCommentRequest(string? Comment);

public record DbUser(int Id, string Username, string FullName, string Email, string Role, string LastLogin);

public record UpdateRoleRequest(string Role);

public record UpdateNameRequest(string FullName);

public class UserNotificationPref
{
    public string LogType { get; set; } = "";
    public bool Enabled { get; set; }
}

public record UpdateNotificationPrefsRequest(List<UserNotificationPref> Prefs);
