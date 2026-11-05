using System.Data;
using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
namespace AQUAR.Api;

public sealed class PayrollService(AppDb db, AccessService access, IConfiguration configuration)
{
    public void Enabled()
    {
        if (!configuration.GetValue<bool>("Payroll:Enabled"))
            throw new ApiException(503, "Bảng lương tháng chưa được bật. Cần xác nhận migration và bật Payroll__Enabled.");
    }
    public static void Period(int year, int month)
    {
        if(year is <2000 or >2100 || month is <1 or >12) throw new ApiException(400,"Tháng/năm không hợp lệ.");
    }
    public static void Precision(params decimal[] values)
    {
        if(values.Any(x=>decimal.Round(x,4)!=x))throw new ApiException(400,"Số tiền và cấu hình hỗ trợ tối đa 4 chữ số thập phân.");
    }
    public async Task RequireView(ClaimsPrincipal user, int staffId)
    {
        await access.Require(user,staffId);
        if(access.Id(user)!=staffId && !user.IsInRole("Admin") &&
           !await db.SalaryConfigurations.AnyAsync(x=>x.StaffId==staffId&&x.ManagerCanView))
            throw new ApiException(403,"Admin chưa cấp quyền xem lương nhân viên này cho Manager.");
    }
    public static PayrollRecord Compute(SalaryConfiguration c, IEnumerable<Attendance> shifts, PayrollCalculationRequest r, int actor)
    {
        Period(r.Year,r.Month);
        Precision(c.MonthlySalary,c.StandardDays,c.HoursPerDay,c.OvertimeMultiplier,r.ApprovedOvertimeHours,r.Allowances,r.Deductions,r.UnpaidLeaveDays??0);
        if(c.MonthlySalary<0||c.MonthlySalary>1000000000000m||c.StandardDays is <1 or >31||c.HoursPerDay is <1 or >24||c.OvertimeMultiplier is <1 or >10||c.Formula is not ("EquivalentDays" or "DailyCapped"))
            throw new ApiException(400,"Cấu hình lương không hợp lệ.");
        if(r.ApprovedOvertimeHours is <0 or >744||r.Allowances is <0 or >1000000000000m||r.Deductions is <0 or >1000000000000m||r.LateCount is <0 or >1000||r.UnpaidLeaveDays is <0 or >31)
            throw new ApiException(400,"Khoản điều chỉnh không hợp lệ.");
        if((r.ApprovedOvertimeHours>0||r.Allowances>0||r.Deductions>0||r.LateCount.HasValue||r.UnpaidLeaveDays.HasValue)&&string.IsNullOrWhiteSpace(r.InputNote))
            throw new ApiException(400,"Cần ghi nguồn/phê duyệt cho các khoản điều chỉnh nhập tay.");
        var (from,to)=ReportService.Period("month",new DateOnly(r.Year,r.Month,1));
        var rows=shifts.Where(a=>a.StaffId==c.StaffId&&a.CheckInUtc<to&&(a.CheckOutUtc==null||a.CheckOutUtc>from)).ToList();
        var closed=rows.Where(a=>a.CheckOutUtc.HasValue).OrderBy(a=>a.CheckInUtc).ToList();
        DateTime? previousEnd=null;
        var daily=new Dictionary<DateTime,decimal>();
        foreach(var a in closed)
        {
            var start=a.CheckInUtc<from?from:a.CheckInUtc;
            var end=a.CheckOutUtc!.Value>to?to:a.CheckOutUtc.Value;
            if(end<=start) throw new ApiException(409,"Ca không hợp lệ; cần kiểm tra dữ liệu trước khi tính lương.");
            if(previousEnd.HasValue&&start<previousEnd) throw new ApiException(409,"Ca bị chồng lấn; không tự cộng trùng giờ công.");
            previousEnd=end;
            while(start<end)
            {
                var day=start.AddHours(7).Date;
                var next=day.AddDays(1).AddHours(-7);
                var stop=end<next?end:next;
                daily[day]=daily.GetValueOrDefault(day)+(decimal)(stop-start).Ticks/TimeSpan.TicksPerHour;
                start=stop;
            }
        }
        var hours=daily.Values.Sum();
        if(r.ApprovedOvertimeHours>hours) throw new ApiException(400,"Giờ tăng ca đã duyệt không được vượt giờ chấm công hợp lệ.");
        var regular=hours-r.ApprovedOvertimeHours;
        var equivalent=regular/c.HoursPerDay;
        if(c.Formula=="DailyCapped") equivalent=Math.Min(equivalent,daily.Values.Sum(h=>Math.Min(h/c.HoursPerDay,1m)));
        var days=Math.Min(equivalent,c.StandardDays);
        // Compute from full decimal precision; round only monetary results to cents.
        var pay=Math.Round(c.MonthlySalary/c.StandardDays*days,2,MidpointRounding.AwayFromZero);
        var overtime=Math.Round(c.MonthlySalary/c.StandardDays/c.HoursPerDay*c.OvertimeMultiplier*r.ApprovedOvertimeHours,2,MidpointRounding.AwayFromZero);
        var net=pay+overtime+r.Allowances-r.Deductions;
        if(net<0) throw new ApiException(400,"Khấu trừ vượt tổng thu nhập; cần kiểm tra lại.");
        if(new[]{pay,overtime,net}.Any(x=>x>=100000000000000m)) throw new ApiException(400,"Kết quả vượt giới hạn lưu trữ tiền tệ.");
        return new PayrollRecord{StaffId=c.StaffId,Year=r.Year,Month=r.Month,MonthlySalary=c.MonthlySalary,StandardDays=c.StandardDays,HoursPerDay=c.HoursPerDay,OvertimeMultiplier=c.OvertimeMultiplier,Formula=c.Formula,
            ValidHours=Math.Round(hours,4),ValidDays=Math.Round(days,4),OpenShifts=rows.Count(a=>!a.CheckOutUtc.HasValue),LateCount=r.LateCount,UnpaidLeaveDays=r.UnpaidLeaveDays,
            ApprovedOvertimeHours=r.ApprovedOvertimeHours,Allowances=r.Allowances,Deductions=r.Deductions,InputNote=r.InputNote?.Trim()??"",AttendancePay=pay,OvertimePay=overtime,NetPay=net,CalculatedAtUtc=DateTime.UtcNow,CalculatedBy=actor};
    }
    public async Task<PayrollRecord> Calculate(int id,PayrollCalculationRequest r,int actor)
    {
        Period(r.Year,r.Month);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var c=await db.SalaryConfigurations.AsNoTracking().SingleOrDefaultAsync(x=>x.StaffId==id)??throw new ApiException(409,"Chưa cấu hình lương nhân viên.");
        var old=await db.PayrollRecords.SingleOrDefaultAsync(x=>x.StaffId==id&&x.Year==r.Year&&x.Month==r.Month);
        if(old!=null&&(old.State!="Draft"||old.Version!=r.Version)) throw new ApiException(409,"Bảng lương đã duyệt/chốt hoặc phiên bản thay đổi; không ghi đè.");
        var (from,to)=ReportService.Period("month",new DateOnly(r.Year,r.Month,1));
        var rows=await db.Attendances.AsNoTracking().Where(a=>a.StaffId==id&&a.CheckInUtc<to&&(a.CheckOutUtc==null||a.CheckOutUtc>from)).ToListAsync();
        var value=Compute(c,rows,r,actor);
        if(old==null) db.PayrollRecords.Add(value);
        else {value.Id=old.Id;db.Entry(old).CurrentValues.SetValues(value);}
        await db.SaveChangesAsync();await tx.CommitAsync();return value;
    }
    public static object View(PayrollRecord p)
    {
        static string D(decimal x)=>x.ToString(CultureInfo.InvariantCulture);
        return new {p.Id,p.StaffId,p.Year,p.Month,p.State,p.Version,p.Formula,monthlySalary=D(p.MonthlySalary),standardDays=D(p.StandardDays),hoursPerDay=D(p.HoursPerDay),overtimeMultiplier=D(p.OvertimeMultiplier),validHours=D(p.ValidHours),validDays=D(p.ValidDays),p.OpenShifts,p.LateCount,unpaidLeaveDays=p.UnpaidLeaveDays.HasValue?D(p.UnpaidLeaveDays.Value):null,approvedOvertimeHours=D(p.ApprovedOvertimeHours),allowances=D(p.Allowances),deductions=D(p.Deductions),attendancePay=D(p.AttendancePay),overtimePay=D(p.OvertimePay),netPay=D(p.NetPay),p.InputNote,p.CalculatedAtUtc,p.ApprovedAtUtc,p.ClosedAtUtc,
            note="VND. Ngày công quy đổi từ giờ ca đã đóng (UTC+7). OT nhập là phần giờ đã duyệt nằm trong giờ chấm công, được trừ khỏi giờ thường. Nghỉ không lương/đi muộn chưa có dữ liệu hiển thị chưa xác định; không trừ lương thêm lần nữa. Chưa tính thuế/bảo hiểm nếu chưa nhập khấu trừ."};
    }
    public static object ConfigurationView(SalaryConfiguration c)=>new{c.Id,c.StaffId,
        monthlySalary=c.MonthlySalary.ToString(CultureInfo.InvariantCulture),standardDays=c.StandardDays.ToString(CultureInfo.InvariantCulture),
        hoursPerDay=c.HoursPerDay.ToString(CultureInfo.InvariantCulture),overtimeMultiplier=c.OvertimeMultiplier.ToString(CultureInfo.InvariantCulture),c.Formula,c.ManagerCanView,c.Version};
}
