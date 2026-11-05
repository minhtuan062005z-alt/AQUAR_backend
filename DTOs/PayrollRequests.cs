using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace AQUAR.Api;

[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public record SalaryConfigurationRequest(
    [Range(typeof(decimal), "0", "1000000000000")] decimal MonthlySalary,
    [Range(typeof(decimal), "1", "31")] decimal StandardDays,
    [Range(typeof(decimal), "1", "24")] decimal HoursPerDay,
    [Range(typeof(decimal), "1", "10")] decimal OvertimeMultiplier,
    [Required] string Formula, bool ManagerCanView, Guid? Version);

[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public record PayrollCalculationRequest(
    [Range(2000, 2100)] int Year, [Range(1, 12)] int Month,
    [Range(typeof(decimal), "0", "744")] decimal ApprovedOvertimeHours,
    [Range(typeof(decimal), "0", "1000000000000")] decimal Allowances,
    [Range(typeof(decimal), "0", "1000000000000")] decimal Deductions,
    [Range(0, 1000)] int? LateCount,
    [Range(typeof(decimal), "0", "31")] decimal? UnpaidLeaveDays,
    [StringLength(2000)] string? InputNote, Guid? Version);
public record PayrollTransitionRequest(Guid Version);
