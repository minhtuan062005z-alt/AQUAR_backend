using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AQUAR.Api;

[ApiController,Authorize,Route("api/monthly-payroll")]
public class PayrollController(AppDb db,AccessService access,PayrollService payroll):ControllerBase
{
    [HttpGet("configuration/{staffId:int}"),Authorize(Roles="Admin")]
    public async Task<IActionResult> Configuration(int staffId)
    {
        payroll.Enabled();var c=await db.SalaryConfigurations.AsNoTracking().SingleOrDefaultAsync(x=>x.StaffId==staffId);
        return c==null?NotFound(new{message="Chưa cấu hình lương."}):Ok(PayrollService.ConfigurationView(c));
    }
    [HttpPut("configuration/{staffId:int}"),Authorize(Roles="Admin")]
    public async Task<IActionResult> Configure(int staffId,SalaryConfigurationRequest r)
    {
        payroll.Enabled();
        PayrollService.Precision(r.MonthlySalary,r.StandardDays,r.HoursPerDay,r.OvertimeMultiplier);
        if(r.Formula is not ("EquivalentDays" or "DailyCapped"))return BadRequest(new{message="Công thức phải là EquivalentDays hoặc DailyCapped."});
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if(!await db.Staff.AnyAsync(s=>s.Id==staffId))return NotFound();
        var c=await db.SalaryConfigurations.SingleOrDefaultAsync(x=>x.StaffId==staffId);
        if(c!=null&&c.Version!=r.Version)return Conflict(new{message="Cấu hình đã thay đổi; tải lại trước khi lưu."});
        if(c==null){c=new(){StaffId=staffId};db.SalaryConfigurations.Add(c);}
        c.MonthlySalary=r.MonthlySalary;c.StandardDays=r.StandardDays;c.HoursPerDay=r.HoursPerDay;c.OvertimeMultiplier=r.OvertimeMultiplier;c.Formula=r.Formula;c.ManagerCanView=r.ManagerCanView;c.Version=Guid.NewGuid();
        await db.SaveChangesAsync();await tx.CommitAsync();return Ok(PayrollService.ConfigurationView(c));
    }
    [HttpPost("{staffId:int}/calculate"),Authorize(Roles="Admin")]
    public async Task<IActionResult> Calculate(int staffId,PayrollCalculationRequest r)
    {payroll.Enabled();return Ok(PayrollService.View(await payroll.Calculate(staffId,r,access.Id(User))));}
    [HttpGet]
    public async Task<IActionResult> List(int year,int month,int? staffId)
    {
        payroll.Enabled();PayrollService.Period(year,month);
        var current=access.Id(User);var query=db.PayrollRecords.AsNoTracking().Where(x=>x.Year==year&&x.Month==month);
        if(staffId.HasValue){await payroll.RequireView(User,staffId.Value);query=query.Where(x=>x.StaffId==staffId);}
        else if(!User.IsInRole("Admin"))
        {
            if(User.IsInRole("Manager"))query=query.Where(x=>x.StaffId==current||db.Staff.Any(s=>s.Id==x.StaffId&&s.ManagerId==current)&&db.SalaryConfigurations.Any(c=>c.StaffId==x.StaffId&&c.ManagerCanView));
            else query=query.Where(x=>x.StaffId==current);
        }
        return Ok((await query.OrderBy(x=>x.StaffId).ToListAsync()).Select(PayrollService.View));
    }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {payroll.Enabled();var p=await db.PayrollRecords.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id)??throw new ApiException(404,"Không tìm thấy bảng lương.");await payroll.RequireView(User,p.StaffId);return Ok(PayrollService.View(p));}
    [HttpPost("{id:int}/approve"),Authorize(Roles="Admin")]
    public Task<IActionResult> Approve(int id,PayrollTransitionRequest r)=>Transition(id,r,false);
    [HttpPost("{id:int}/close"),Authorize(Roles="Admin")]
    public Task<IActionResult> Close(int id,PayrollTransitionRequest r)=>Transition(id,r,true);
    private async Task<IActionResult> Transition(int id,PayrollTransitionRequest r,bool close)
    {
        payroll.Enabled();await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var p=await db.PayrollRecords.SingleOrDefaultAsync(x=>x.Id==id);if(p==null)return NotFound();
        if(p.Version!=r.Version||p.State!=(close?"Approved":"Draft"))return Conflict(new{message="Trạng thái hoặc phiên bản bảng lương đã thay đổi."});
        if(p.OpenShifts>0)return Conflict(new{message="Còn ca chưa đóng. Cần xử lý và tính lại trước khi duyệt."});
        if(!close)
        {
            var config=await db.SalaryConfigurations.AsNoTracking().SingleAsync(x=>x.StaffId==p.StaffId);
            var (from,to)=ReportService.Period("month",new DateOnly(p.Year,p.Month,1));
            var shifts=await db.Attendances.AsNoTracking().Where(x=>x.StaffId==p.StaffId&&x.CheckInUtc<to&&(x.CheckOutUtc==null||x.CheckOutUtc>from)).ToListAsync();
            var fresh=PayrollService.Compute(config,shifts,new(p.Year,p.Month,p.ApprovedOvertimeHours,p.Allowances,p.Deductions,p.LateCount,p.UnpaidLeaveDays,p.InputNote,p.Version),p.CalculatedBy);
            if(fresh.ValidHours!=p.ValidHours||fresh.ValidDays!=p.ValidDays||fresh.NetPay!=p.NetPay||fresh.OpenShifts!=p.OpenShifts||fresh.MonthlySalary!=p.MonthlySalary||fresh.StandardDays!=p.StandardDays||fresh.HoursPerDay!=p.HoursPerDay||fresh.OvertimeMultiplier!=p.OvertimeMultiplier||fresh.Formula!=p.Formula)
                return Conflict(new{message="Công hoặc cấu hình lương đã thay đổi. Cần tính lại trước khi duyệt."});
        }
        if(close){p.State="Closed";p.ClosedAtUtc=DateTime.UtcNow;p.ClosedBy=access.Id(User);}
        else{p.State="Approved";p.ApprovedAtUtc=DateTime.UtcNow;p.ApprovedBy=access.Id(User);}
        p.Version=Guid.NewGuid();await db.SaveChangesAsync();await tx.CommitAsync();return Ok(PayrollService.View(p));
    }
    [HttpGet("export"),Authorize(Roles="Admin")]
    public async Task<IActionResult> Export(int year,int month)
    {
        payroll.Enabled();PayrollService.Period(year,month);
        var rows=await db.PayrollRecords.AsNoTracking().Where(x=>x.Year==year&&x.Month==month).OrderBy(x=>x.StaffId).ToListAsync();
        // Numeric identifiers only: no untrusted formula-capable spreadsheet cells.
        var csv=new StringBuilder("StaffId,Year,Month,State,ValidDays,ValidHours,AttendancePay,OvertimePay,Allowances,Deductions,NetPay\r\n");
        foreach(var p in rows)csv.AppendLine(FormattableString.Invariant($"{p.StaffId},{p.Year},{p.Month},{p.State},{p.ValidDays},{p.ValidHours},{p.AttendancePay},{p.OvertimePay},{p.Allowances},{p.Deductions},{p.NetPay}"));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),"text/csv; charset=utf-8",$"AQUAR-payroll-{year}-{month:00}.csv");
    }
}
