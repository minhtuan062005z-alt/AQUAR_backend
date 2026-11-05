namespace AQUAR.Api;

public class SalaryConfiguration
{
    public int Id { get; set; }
    public int StaffId { get; set; }
    public decimal MonthlySalary { get; set; }
    public decimal StandardDays { get; set; }
    public decimal HoursPerDay { get; set; }
    public decimal OvertimeMultiplier { get; set; }
    public string Formula { get; set; } = "EquivalentDays";
    public bool ManagerCanView { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}

public class PayrollRecord
{
    public int Id { get; set; }
    public int StaffId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal MonthlySalary { get; set; }
    public decimal StandardDays { get; set; }
    public decimal HoursPerDay { get; set; }
    public decimal OvertimeMultiplier { get; set; }
    public string Formula { get; set; } = "";
    public decimal ValidHours { get; set; }
    public decimal ValidDays { get; set; }
    public int OpenShifts { get; set; }
    public int? LateCount { get; set; }
    public decimal? UnpaidLeaveDays { get; set; }
    public decimal ApprovedOvertimeHours { get; set; }
    public decimal Allowances { get; set; }
    public decimal Deductions { get; set; }
    public string InputNote { get; set; } = "";
    public decimal AttendancePay { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal NetPay { get; set; }
    public string State { get; set; } = "Draft";
    public DateTime CalculatedAtUtc { get; set; }
    public int CalculatedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public int? ApprovedBy { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public int? ClosedBy { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
