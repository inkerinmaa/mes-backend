namespace MyDashboardApi.Models;

record PkfChRow(
    string OrderNumber, string StartTs, string EndTs, double DurationH,
    double BasaltT, double BinderKg, double WoolT, double WasteKg, double AvgEfficiency);

record EnergyChRow(
    string OrderNumber, string StartTs, string EndTs, double DurationH,
    double TotalGasM3, double TotalElecKwh, double TotalWaterM3);

record WasteChRow(
    string OrderNumber, string StartTs, string EndTs, double DurationH,
    double TrimmingKg, double StartupKg, double RejectedKg, double TotalKg, double WastePct);

public record PkfReportRow(
    string OrderNumber, string? SkuCode, string? SkuName,
    string StartTs, string EndTs, double DurationH,
    double BasaltT, double BinderKg, double WoolT, double WasteKg, double AvgEfficiency);

public record EnergyReportRow(
    string OrderNumber, string? SkuCode, string? SkuName,
    string StartTs, string EndTs, double DurationH,
    double TotalGasM3, double TotalElecKwh, double TotalWaterM3);

public record WasteReportRow(
    string OrderNumber, string? SkuCode, string? SkuName,
    string StartTs, string EndTs, double DurationH,
    double TrimmingKg, double StartupKg, double RejectedKg, double TotalKg, double WastePct);

public record Material(int Id, string Code, string Name, string Unit, decimal StockQuantity);
