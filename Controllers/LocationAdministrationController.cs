using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace AQUAR.Api;

public class CompanyLocationConfiguration
{
    public int Id {get;set;}
    public double? Latitude {get;set;}
    public double? Longitude {get;set;}
    public double RadiusMeters {get;set;}=100;
    public Guid Version {get;set;}=Guid.NewGuid();
    public bool RequireGpsVerification {get;set;}
    public double MaxAccuracyMeters {get;set;}=50;
    public int MaxAgeSeconds {get;set;}=30;
    public DateTime? UpdatedAtUtc {get;set;}
    public int? UpdatedBy {get;set;}
}
public record CompanyLocationRequest(double? Latitude,double? Longitude,double RadiusMeters,Guid? Version,bool RequireGpsVerification=false,double MaxAccuracyMeters=50,int MaxAgeSeconds=30);

[ApiController,Authorize(Roles="Admin,Manager"),Route("api/location-administration")]
public class LocationAdministrationController(AppDb db,AccessService access,IOptions<CompanyLocationOptions> options,IConfiguration configuration):ControllerBase
{
    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts(int? staffId,int take=100,DateOnly? from=null,DateOnly? to=null,string? status=null,int skip=0)
    {
        if(take is <1 or >200||skip<0||skip>100000||from>to||from?.Year is <2000 or >2100||to?.Year is <2000 or >2100||status is not (null or "" or "OUT_OF_RANGE"))return BadRequest(new{message="Bộ lọc không hợp lệ; trạng thái hỗ trợ OUT_OF_RANGE, ngày UTC+7, take 1–200."});
        if(staffId.HasValue)await access.Require(User,staffId.Value);
        var admin=User.IsInRole("Admin");var id=access.Id(User);
        var query=db.AttendanceLocationAlerts.AsNoTracking().Where(x=>admin||x.Location.Attendance.StaffId==id||x.Location.Attendance.Staff.ManagerId==id);
        if(staffId.HasValue)query=query.Where(x=>x.Location.Attendance.StaffId==staffId);
        if(from.HasValue){var start=DateTime.SpecifyKind(from.Value.ToDateTime(TimeOnly.MinValue).AddHours(-7),DateTimeKind.Utc);query=query.Where(x=>x.CreatedAtUtc>=start);}
        if(to.HasValue){var end=DateTime.SpecifyKind(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7),DateTimeKind.Utc);query=query.Where(x=>x.CreatedAtUtc<end);}
        return Ok(await query.OrderByDescending(x=>x.CreatedAtUtc).ThenByDescending(x=>x.Id).Skip(skip).Take(take).Select(x=>new{x.Id,x.Kind,x.CreatedAtUtc,staffId=x.Location.Attendance.StaffId,staffName=x.Location.Attendance.Staff.Name,eventType=x.Location.Event.ToString(),x.Location.Latitude,x.Location.Longitude,x.Location.DistanceMeters,x.Location.RadiusMeters,x.Location.AccuracyMeters,status="OUT_OF_RANGE",reason=x.Kind}).ToListAsync());
    }
    [HttpGet("company"),Authorize(Roles="Admin")]
    public async Task<IActionResult> Company()
    {
        var editable=configuration.GetValue<bool>("CompanyLocation:DatabaseEnabled");
        var saved=editable?await db.CompanyLocationConfigurations.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==1):null;
        return Ok(new{latitude=saved==null?options.Value.Latitude:saved.Latitude,longitude=saved==null?options.Value.Longitude:saved.Longitude,radiusMeters=saved?.RadiusMeters??options.Value.RadiusMeters,version=saved?.Version,editable,requireGpsVerification=saved?.RequireGpsVerification??options.Value.RequireGpsVerification,maxAccuracyMeters=saved?.MaxAccuracyMeters??options.Value.MaxAccuracyMeters,maxAgeSeconds=saved?.MaxAgeSeconds??options.Value.MaxAgeSeconds,updatedAtUtc=saved?.UpdatedAtUtc,source=saved==null?"Environment":"Database"});
    }
    [HttpPut("company"),Authorize(Roles="Admin")]
    public async Task<IActionResult> Update(CompanyLocationRequest r)
    {
        if(!configuration.GetValue<bool>("CompanyLocation:DatabaseEnabled"))throw new ApiException(503,"Chưa bật cấu hình vị trí trong database; cần xác nhận migration trước.");
        var options=new CompanyLocationOptions{Latitude=r.Latitude,Longitude=r.Longitude,RadiusMeters=r.RadiusMeters,RequireGpsVerification=r.RequireGpsVerification,MaxAccuracyMeters=r.MaxAccuracyMeters,MaxAgeSeconds=r.MaxAgeSeconds};
        if(!options.IsValid()||r.RadiusMeters>100000||r.RequireGpsVerification&&!r.Latitude.HasValue)return BadRequest(new{message="Cần đủ cặp tọa độ hợp lệ; bán kính (0,100000], độ chính xác (0,10000], tuổi GPS 5–300 giây. Phải có tọa độ trước khi bắt buộc xác minh."});
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var value=await db.CompanyLocationConfigurations.SingleOrDefaultAsync(x=>x.Id==1);
        if(value!=null&&value.Version!=r.Version)return Conflict(new{message="Cấu hình vị trí đã thay đổi; tải lại trước khi lưu."});
        if(value==null){value=new(){Id=1};db.CompanyLocationConfigurations.Add(value);}
        value.Latitude=r.Latitude;value.Longitude=r.Longitude;value.RadiusMeters=r.RadiusMeters;value.Version=Guid.NewGuid();
        value.RequireGpsVerification=r.RequireGpsVerification;value.MaxAccuracyMeters=r.MaxAccuracyMeters;value.MaxAgeSeconds=r.MaxAgeSeconds;value.UpdatedAtUtc=DateTime.UtcNow;value.UpdatedBy=access.Id(User);
        await db.SaveChangesAsync();await tx.CommitAsync();return await Company();
    }
}
