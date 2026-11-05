using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AQUAR.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGpsVerificationPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "MaxAccuracyMeters",
                table: "CompanyLocationConfigurations",
                type: "float",
                nullable: false,
                defaultValue: 50.0);

            migrationBuilder.AddColumn<int>(
                name: "MaxAgeSeconds",
                table: "CompanyLocationConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<bool>(
                name: "RequireGpsVerification",
                table: "CompanyLocationConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "CompanyLocationConfigurations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "CompanyLocationConfigurations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AccuracyMeters",
                table: "AttendanceLocations",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CapturedAtUtc",
                table: "AttendanceLocations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "AttendanceLocations",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationResult",
                table: "AttendanceLocations",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxAccuracyMeters",
                table: "CompanyLocationConfigurations");

            migrationBuilder.DropColumn(
                name: "MaxAgeSeconds",
                table: "CompanyLocationConfigurations");

            migrationBuilder.DropColumn(
                name: "RequireGpsVerification",
                table: "CompanyLocationConfigurations");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "CompanyLocationConfigurations");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "CompanyLocationConfigurations");

            migrationBuilder.DropColumn(
                name: "AccuracyMeters",
                table: "AttendanceLocations");

            migrationBuilder.DropColumn(
                name: "CapturedAtUtc",
                table: "AttendanceLocations");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "AttendanceLocations");

            migrationBuilder.DropColumn(
                name: "VerificationResult",
                table: "AttendanceLocations");
        }
    }
}
