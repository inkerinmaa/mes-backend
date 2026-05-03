using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public interface IReportRepository
{
    Task<List<PkfReportRow>>    GetPkfByPeriodAsync(int lineId, string startDate, string endDate);
    Task<List<PkfReportRow>>    GetPkfByOrderAsync(string orderNumber);
    Task<List<EnergyReportRow>> GetEnergyByPeriodAsync(int lineId, string startDate, string endDate);
    Task<List<EnergyReportRow>> GetEnergyByOrderAsync(string orderNumber);
    Task<List<WasteReportRow>>  GetWasteByPeriodAsync(int lineId, string startDate, string endDate);
    Task<List<WasteReportRow>>  GetWasteByOrderAsync(string orderNumber);
}
