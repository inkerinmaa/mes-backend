namespace MyDashboardApi.Models;

public record StatMetric(int Value, int Variation);

public record StatMetricDecimal(double Value, double Variation);

public record DashboardStats(
    StatMetricDecimal TotalTonnes,
    StatMetricDecimal LineUptime,
    StatMetricDecimal WastePercentage,
    StatMetric Orders);

public record EfficiencyPoint(string Date, double Value);

public record ProductionEvent(int Id, string Time, string Event, string Line, string Severity);

public record MachineState(string Timestamp, string State, int DurationMinutes);

public record Avatar(string Src, string Alt);
