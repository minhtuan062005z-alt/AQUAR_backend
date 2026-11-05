using Microsoft.EntityFrameworkCore;
namespace AQUAR.Api;
public class AppDb(DbContextOptions<AppDb> options):DbContext(options) {
 public DbSet<SalaryConfiguration> SalaryConfigurations=>Set<SalaryConfiguration>();
 public DbSet<CompanyLocationConfiguration> CompanyLocationConfigurations=>Set<CompanyLocationConfiguration>();
 public DbSet<PayrollRecord> PayrollRecords=>Set<PayrollRecord>();
 public DbSet<AttendanceLocation> AttendanceLocations=>Set<AttendanceLocation>();
 public DbSet<AttendanceLocationAlert> AttendanceLocationAlerts=>Set<AttendanceLocationAlert>();
 public DbSet<Staff> Staff=>Set<Staff>(); public DbSet<Attendance> Attendances=>Set<Attendance>(); public DbSet<Department> Departments=>Set<Department>();
 protected override void OnModelCreating(ModelBuilder m) {
 m.Entity<CompanyLocationConfiguration>().Property(x=>x.Id).ValueGeneratedNever();
 m.Entity<CompanyLocationConfiguration>().Property(x=>x.Version).IsConcurrencyToken();
 m.Entity<CompanyLocationConfiguration>().Property(x=>x.MaxAccuracyMeters).HasDefaultValue(50d);
 m.Entity<CompanyLocationConfiguration>().Property(x=>x.MaxAgeSeconds).HasDefaultValue(30);
 m.Entity<SalaryConfiguration>().HasOne<Staff>().WithMany().HasForeignKey(x=>x.StaffId).OnDelete(DeleteBehavior.Restrict);
 m.Entity<SalaryConfiguration>().HasIndex(x=>x.StaffId).IsUnique();
 m.Entity<SalaryConfiguration>().Property(x=>x.Version).IsConcurrencyToken();
 m.Entity<SalaryConfiguration>().Property(x=>x.Formula).HasMaxLength(32);
 m.Entity<PayrollRecord>().HasOne<Staff>().WithMany().HasForeignKey(x=>x.StaffId).OnDelete(DeleteBehavior.Restrict);
 m.Entity<PayrollRecord>().HasIndex(x=>new{x.StaffId,x.Year,x.Month}).IsUnique();
 m.Entity<PayrollRecord>().Property(x=>x.Version).IsConcurrencyToken();
 m.Entity<PayrollRecord>().Property(x=>x.Formula).HasMaxLength(32);
 m.Entity<PayrollRecord>().Property(x=>x.State).HasMaxLength(16);
 m.Entity<PayrollRecord>().Property(x=>x.InputNote).HasMaxLength(2000);
 foreach(var entity in new[]{typeof(SalaryConfiguration),typeof(PayrollRecord)})
 foreach(var property in entity.GetProperties().Where(p=>p.PropertyType==typeof(decimal)||p.PropertyType==typeof(decimal?)))
 m.Entity(entity).Property(property.Name).HasPrecision(18,4);
 m.Entity<AttendanceLocation>().HasOne(x=>x.Attendance).WithMany(x=>x.Locations).HasForeignKey(x=>x.AttendanceId).OnDelete(DeleteBehavior.Restrict);
 m.Entity<AttendanceLocation>().HasIndex(x=>new{x.AttendanceId,x.Event}).IsUnique();
 m.Entity<AttendanceLocation>().Property(x=>x.VerificationReason).HasMaxLength(64);
 m.Entity<AttendanceLocation>().Property(x=>x.VerificationResult).HasMaxLength(32);
 m.Entity<AttendanceLocation>().Property(x=>x.Method).HasMaxLength(16);
 m.Entity<AttendanceLocationAlert>().HasOne(x=>x.Location).WithOne(x=>x.Alert).HasForeignKey<AttendanceLocationAlert>(x=>x.AttendanceLocationId).OnDelete(DeleteBehavior.Restrict);
 m.Entity<AttendanceLocationAlert>().Property(x=>x.Kind).HasMaxLength(64);
 m.Entity<Staff>().Property(x=>x.PasswordHash).IsConcurrencyToken();
 m.Entity<Staff>().Property(x=>x.TokenVersion).IsConcurrencyToken();
 m.Entity<Staff>().HasIndex(x=>x.Email).IsUnique();
 m.Entity<Staff>().Property(x=>x.Email).HasMaxLength(254);
 m.Entity<Staff>().Property(x=>x.Name).HasMaxLength(200);
 m.Entity<Staff>().Property(x=>x.HourlyRate).HasPrecision(18,2);
 m.Entity<Staff>().HasOne<Staff>().WithMany().HasForeignKey(x=>x.ManagerId).OnDelete(DeleteBehavior.Restrict);
 m.Entity<Staff>().HasOne<Department>().WithMany().HasForeignKey(x=>x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
 m.Entity<Department>().Property(x=>x.Name).HasMaxLength(200);
 m.Entity<Department>().HasIndex(x=>x.Name).IsUnique();
 m.Entity<Attendance>().HasOne(x=>x.Staff).WithMany().HasForeignKey(x=>x.StaffId).OnDelete(DeleteBehavior.Restrict);
 m.Entity<Attendance>().HasIndex(x=>new{x.StaffId,x.CheckInUtc});
 m.Entity<Attendance>().HasIndex(x=>x.StaffId).IsUnique().HasFilter("[CheckOutUtc] IS NULL");
 }
}

