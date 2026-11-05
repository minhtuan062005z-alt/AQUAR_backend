using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace AQUAR.Api;

public sealed class CompanyLocationOptions
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double RadiusMeters { get; set; } = 100;
    public bool RequireGpsVerification { get; set; }
    public double MaxAccuracyMeters { get; set; } = 50;
    public int MaxAgeSeconds { get; set; } = 30;
    public bool IsValid() => double.IsFinite(RadiusMeters) && RadiusMeters > 0 &&
        double.IsFinite(MaxAccuracyMeters) && MaxAccuracyMeters > 0 && MaxAccuracyMeters <= 10000 && MaxAgeSeconds is >= 5 and <= 300 &&
        ((Latitude == null && Longitude == null) ||
         (Latitude.HasValue && Longitude.HasValue && AttendanceLocationService.ValidCoordinates(Latitude.Value, Longitude.Value)));
}

public sealed class AttendanceLocationService(IOptions<CompanyLocationOptions> options,AppDb? db=null,IConfiguration? configuration=null)
{
    public const double EarthRadiusMeters = 6371008.8;
    public static bool ValidCoordinates(double latitude, double longitude) =>
        double.IsFinite(latitude) && double.IsFinite(longitude) && latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        static double Radians(double degrees) => degrees * Math.PI / 180;
        var a = Math.Pow(Math.Sin(Radians(lat2 - lat1) / 2), 2) +
            Math.Cos(Radians(lat1)) * Math.Cos(Radians(lat2)) * Math.Pow(Math.Sin(Radians(lon2 - lon1) / 2), 2);
        return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(Math.Clamp(a, 0, 1)), Math.Sqrt(1 - Math.Clamp(a, 0, 1)));
    }

    public AttendanceLocation Capture(AttendanceEvent eventType, double? latitude, double? longitude, DateTime serverTime,double? accuracy=null,DateTime? capturedAtUtc=null,string? method=null)
        => CaptureWithOptions(eventType,latitude,longitude,serverTime,options.Value,accuracy,capturedAtUtc,method);

    public async Task<CompanyLocationOptions> PolicyAsync()
    {
        var cfg=options.Value;
        if(db!=null&&configuration?.GetValue<bool>("CompanyLocation:DatabaseEnabled")==true)
        {
            var saved=await db.CompanyLocationConfigurations.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==1);
            if(saved!=null)cfg=new(){Latitude=saved.Latitude,Longitude=saved.Longitude,RadiusMeters=saved.RadiusMeters,RequireGpsVerification=saved.RequireGpsVerification,MaxAccuracyMeters=saved.MaxAccuracyMeters,MaxAgeSeconds=saved.MaxAgeSeconds};
        }
        return cfg;
    }

    public async Task<AttendanceLocation> CaptureAsync(AttendanceEvent eventType,double? latitude,double? longitude,DateTime serverTime,double? accuracy=null,DateTime? capturedAtUtc=null,string? method=null)
        => CaptureWithOptions(eventType,latitude,longitude,serverTime,await PolicyAsync(),accuracy,capturedAtUtc,method);

    private static AttendanceLocation CaptureWithOptions(AttendanceEvent eventType,double? latitude,double? longitude,DateTime serverTime,CompanyLocationOptions cfg,double? accuracy,DateTime? capturedAtUtc,string? method)
    {
        if (latitude.HasValue != longitude.HasValue ||
            (latitude.HasValue && !ValidCoordinates(latitude.Value, longitude!.Value)))
            throw new ApiException(400, "GPS cần đủ latitude [-90,90] và longitude [-180,180], là các số hữu hạn.");
        if(accuracy.HasValue&&(!double.IsFinite(accuracy.Value)||accuracy.Value<0||accuracy.Value>100000)||
            !latitude.HasValue&&(accuracy.HasValue||capturedAtUtc.HasValue)||capturedAtUtc?.Kind==DateTimeKind.Unspecified)
            throw new ApiException(400,"Accuracy phải là số hữu hạn không âm, đi cùng tọa độ; capturedAtUtc phải có múi giờ.");
        var location = new AttendanceLocation {
            Event = eventType, Latitude = latitude, Longitude = longitude, RecordedAtUtc = serverTime,
            Status = LocationStatus.Unverified, VerificationReason = "NoGps",
            CompanyLatitude = cfg.Latitude, CompanyLongitude = cfg.Longitude, RadiusMeters = cfg.RadiusMeters,
            AccuracyMeters=accuracy,CapturedAtUtc=capturedAtUtc?.ToUniversalTime(),Method=method,VerificationResult="GPS_UNAVAILABLE"
        };
        if (!cfg.Latitude.HasValue || !cfg.Longitude.HasValue) { location.VerificationReason = "CompanyNotConfigured";location.VerificationResult="NOT_CONFIGURED"; }
        else if(latitude.HasValue)
        {
            location.DistanceMeters = DistanceMeters(cfg.Latitude.Value, cfg.Longitude.Value, latitude.Value, longitude!.Value);
            if(!accuracy.HasValue){location.VerificationResult="NOT_VERIFIED";location.VerificationReason="MissingAccuracy";}
            else if(!capturedAtUtc.HasValue){location.VerificationResult="NOT_VERIFIED";location.VerificationReason="MissingCaptureTime";}
            else if(location.CapturedAtUtc>serverTime.AddSeconds(5)||serverTime-location.CapturedAtUtc>TimeSpan.FromSeconds(cfg.MaxAgeSeconds)){location.VerificationResult="NOT_VERIFIED";location.VerificationReason="StaleOrFuturePosition";}
            else if(accuracy>cfg.MaxAccuracyMeters){location.VerificationResult="LOW_ACCURACY";location.VerificationReason="AccuracyAboveLimit";}
            else if(location.DistanceMeters>cfg.RadiusMeters)
            {
                location.Status=LocationStatus.OutOfRange;location.VerificationResult="OUT_OF_RANGE";location.VerificationReason="OutsideCompanyRadius";
                location.Alert=new(){CreatedAtUtc=serverTime,Kind="OutsideCompanyRadius"};
            }
            // Conservative acceptance: the whole reported accuracy circle must fit.
            else if(location.DistanceMeters+accuracy>cfg.RadiusMeters){location.VerificationResult="LOW_ACCURACY";location.VerificationReason="AccuracyOverlapsBoundary";}
            else {location.Status=LocationStatus.InRange;location.VerificationResult="VERIFIED";location.VerificationReason="ClientCoordinatesCompared";}
        }
        if(cfg.RequireGpsVerification&&location.VerificationResult!="VERIFIED")throw new LocationPolicyException(location);
        return location;
    }

    public static object View(AttendanceLocation? location) => new {
        latitude = location?.Latitude, longitude = location?.Longitude, recordedAtUtc = location?.RecordedAtUtc,
        status = (location?.Status ?? LocationStatus.Unverified).ToString(), distanceMeters = location?.DistanceMeters,
        radiusMeters = location?.RadiusMeters, verificationReason = location?.VerificationReason ?? "NoLocationRecord",
        verificationStatus=Result(location),accuracy=location?.AccuracyMeters,capturedAtUtc=location?.CapturedAtUtc,method=location?.Method
    };
    public static string Result(AttendanceLocation? l)=>l?.VerificationResult??(l?.Status switch{
        LocationStatus.InRange=>"VERIFIED",LocationStatus.OutOfRange=>"OUT_OF_RANGE",_=>l?.VerificationReason=="CompanyNotConfigured"?"NOT_CONFIGURED":"NOT_VERIFIED"});
}

public sealed class LocationPolicyException(AttendanceLocation location):ApiException(422,"Chính sách công ty yêu cầu vị trí hợp lệ; lượt chấm công chưa được ghi nhận.")
{public AttendanceLocation Location{get;}=location;}
