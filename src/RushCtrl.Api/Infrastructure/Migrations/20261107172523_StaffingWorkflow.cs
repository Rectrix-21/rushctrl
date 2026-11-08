using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RushCtrl.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StaffingWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "AvailabilityWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvailabilityWindows", x => x.Id);
                    table.CheckConstraint("CK_Availability_Range", "\"EndsAt\" > \"StartsAt\"");
                    table.ForeignKey(
                        name: "FK_AvailabilityWindows_Locations_OrganizationId_LocationId",
                        columns: x => new { x.OrganizationId, x.LocationId },
                        principalTable: "Locations",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvailabilityWindows_Memberships_OrganizationId_UserId",
                        columns: x => new { x.OrganizationId, x.UserId },
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                    table.UniqueConstraint("AK_Skills_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_Skills_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeSkills",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeSkills", x => new { x.OrganizationId, x.UserId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_EmployeeSkills_Memberships_OrganizationId_UserId",
                        columns: x => new { x.OrganizationId, x.UserId },
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeSkills_Skills_OrganizationId_SkillId",
                        columns: x => new { x.OrganizationId, x.SkillId },
                        principalTable: "Skills",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssigneeUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                    table.UniqueConstraint("AK_Shifts_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Shift_Range", "\"EndsAt\" > \"StartsAt\"");
                    table.ForeignKey(
                        name: "FK_Shifts_Locations_OrganizationId_LocationId",
                        columns: x => new { x.OrganizationId, x.LocationId },
                        principalTable: "Locations",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Shifts_Memberships_OrganizationId_AssigneeUserId",
                        columns: x => new { x.OrganizationId, x.AssigneeUserId },
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Shifts_Skills_OrganizationId_SkillId",
                        columns: x => new { x.OrganizationId, x.SkillId },
                        principalTable: "Skills",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffingRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequiredCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffingRequirements", x => x.Id);
                    table.CheckConstraint("CK_Requirement_Count", "\"RequiredCount\" > 0 AND \"RequiredCount\" <= 100");
                    table.CheckConstraint("CK_Requirement_Range", "\"EndsAt\" > \"StartsAt\"");
                    table.ForeignKey(
                        name: "FK_StaffingRequirements_Locations_OrganizationId_LocationId",
                        columns: x => new { x.OrganizationId, x.LocationId },
                        principalTable: "Locations",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffingRequirements_Skills_OrganizationId_SkillId",
                        columns: x => new { x.OrganizationId, x.SkillId },
                        principalTable: "Skills",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CoverageRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverageRequests", x => x.Id);
                    table.CheckConstraint("CK_Coverage_Status", "\"Status\" = 'Pending'");
                    table.ForeignKey(
                        name: "FK_CoverageRequests_Memberships_OrganizationId_RequesterUserId",
                        columns: x => new { x.OrganizationId, x.RequesterUserId },
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CoverageRequests_Shifts_OrganizationId_ShiftId",
                        columns: x => new { x.OrganizationId, x.ShiftId },
                        principalTable: "Shifts",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityWindows_OrganizationId_LocationId",
                table: "AvailabilityWindows",
                columns: new[] { "OrganizationId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityWindows_OrganizationId_UserId_StartsAt",
                table: "AvailabilityWindows",
                columns: new[] { "OrganizationId", "UserId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRequests_OrganizationId_RequesterUserId",
                table: "CoverageRequests",
                columns: new[] { "OrganizationId", "RequesterUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRequests_OrganizationId_ShiftId",
                table: "CoverageRequests",
                columns: new[] { "OrganizationId", "ShiftId" });

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRequests_ShiftId",
                table: "CoverageRequests",
                column: "ShiftId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSkills_OrganizationId_SkillId",
                table: "EmployeeSkills",
                columns: new[] { "OrganizationId", "SkillId" });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_AssigneeUserId_StartsAt",
                table: "Shifts",
                columns: new[] { "AssigneeUserId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_OrganizationId_AssigneeUserId",
                table: "Shifts",
                columns: new[] { "OrganizationId", "AssigneeUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_OrganizationId_LocationId_StartsAt",
                table: "Shifts",
                columns: new[] { "OrganizationId", "LocationId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_OrganizationId_SkillId",
                table: "Shifts",
                columns: new[] { "OrganizationId", "SkillId" });

            migrationBuilder.CreateIndex(
                name: "IX_Skills_OrganizationId_Name",
                table: "Skills",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffingRequirements_OrganizationId_LocationId_StartsAt",
                table: "StaffingRequirements",
                columns: new[] { "OrganizationId", "LocationId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffingRequirements_OrganizationId_SkillId",
                table: "StaffingRequirements",
                columns: new[] { "OrganizationId", "SkillId" });

            migrationBuilder.Sql("""
                ALTER TABLE "Shifts" ADD CONSTRAINT "EX_Shifts_EmployeeOverlap"
                EXCLUDE USING gist ("AssigneeUserId" WITH =, tstzrange("StartsAt", "EndsAt", '[)') WITH &&)
                WHERE ("AssigneeUserId" IS NOT NULL);
                ALTER TABLE "StaffingRequirements" ADD CONSTRAINT "EX_Requirements_Overlap"
                EXCLUDE USING gist ("OrganizationId" WITH =, "LocationId" WITH =, "SkillId" WITH =,
                    tstzrange("StartsAt", "EndsAt", '[)') WITH &&);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvailabilityWindows");

            migrationBuilder.DropTable(
                name: "CoverageRequests");

            migrationBuilder.DropTable(
                name: "EmployeeSkills");

            migrationBuilder.DropTable(
                name: "StaffingRequirements");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,");
        }
    }
}
