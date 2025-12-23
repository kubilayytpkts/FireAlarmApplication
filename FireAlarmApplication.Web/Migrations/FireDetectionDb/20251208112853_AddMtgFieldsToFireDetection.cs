using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FireAlarmApplication.Web.Migrations.FireDetectionDb
{
    /// <inheritdoc />
    public partial class AddMtgFieldsToFireDetection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfidenceLevel",
                schema: "fire_detection",
                table: "fire_detections",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Probability",
                schema: "fire_detection",
                table: "fire_detections",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfidenceLevel",
                schema: "fire_detection",
                table: "fire_detections");

            migrationBuilder.DropColumn(
                name: "Probability",
                schema: "fire_detection",
                table: "fire_detections");
        }
    }
}
