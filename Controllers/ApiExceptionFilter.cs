using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AQUAR.Api;

// Expected validation/access errors are HTTP responses, not server failures.
public sealed class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        // SQL Server serializable transactions/unique indexes arbitrate concurrent attendance.
        // Return a conflict for the losing request; never retry a write automatically.
        var sql=context.Exception as Microsoft.Data.SqlClient.SqlException??context.Exception.InnerException as Microsoft.Data.SqlClient.SqlException;
        if(sql?.Number is 2601 or 2627 or 1205){context.Result=new ConflictObjectResult(new{message="Dữ liệu vừa thay đổi hoặc thao tác bị trùng. Hãy tải lại trạng thái; không tự gửi lại."});context.ExceptionHandled=true;return;}
        if(context.Exception is LocationPolicyException gps){context.Result=new ObjectResult(new{message=gps.Message,code="GPS_VERIFICATION_REQUIRED",verification=AttendanceLocationService.View(gps.Location)}){StatusCode=422};context.ExceptionHandled=true;return;}
        if (context.Exception is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            context.Result = new ConflictObjectResult(new { message = "Tài khoản vừa được cập nhật ở phiên khác. Vui lòng đăng nhập lại và thử lại." });
            context.ExceptionHandled = true;
            return;
        }
        if (context.Exception is not ApiException error) return;
        context.Result = new ObjectResult(new { message = error.Message }) { StatusCode = error.Status };
        context.ExceptionHandled = true;
    }
}
