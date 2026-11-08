using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RushCtrl.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoverageResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CoverageRequests_ShiftId",
                table: "CoverageRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Coverage_Status",
                table: "CoverageRequests");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "CoverageRequests",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Standard");

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacementUserId",
                table: "CoverageRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResolvedAt",
                table: "CoverageRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResolvedByUserId",
                table: "CoverageRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "CoverageRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("UPDATE \"CoverageRequests\" SET \"Version\" = gen_random_uuid()");

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Generation = table.Column<Guid>(type: "uuid", nullable: true),
                    SubjectUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Before = table.Column<string>(type: "text", nullable: true),
                    After = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => new { x.OrganizationId, x.Number });
                    table.ForeignKey(
                        name: "FK_AuditEntries_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditEntries_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SimulationWorkspaces",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Generation = table.Column<Guid>(type: "uuid", nullable: false),
                    Week = table.Column<DateOnly>(type: "date", nullable: false),
                    Applied = table.Column<bool>(type: "boolean", nullable: false),
                    Pristine = table.Column<bool>(type: "boolean", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OtherLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    AbsenceShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandbyShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberIds = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationWorkspaces", x => x.OrganizationId);
                    table.ForeignKey(
                        name: "FK_SimulationWorkspaces_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SimulationWorkspaces_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SimulationWorkspaces_Organizations_SourceOrganizationId",
                        column: x => x.SourceOrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRequests_OrganizationId_ReplacementUserId",
                table: "CoverageRequests",
                columns: new[] { "OrganizationId", "ReplacementUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRequests_ResolvedByUserId",
                table: "CoverageRequests",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRequests_ShiftId",
                table: "CoverageRequests",
                column: "ShiftId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Coverage_Kind",
                table: "CoverageRequests",
                sql: "\"Kind\" IN ('Standard','SimulationCallout')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Coverage_Resolution",
                table: "CoverageRequests",
                sql: "(\"Status\" = 'Pending' AND \"ResolvedAt\" IS NULL AND \"ResolvedByUserId\" IS NULL AND \"ReplacementUserId\" IS NULL) OR (\"Status\" <> 'Pending' AND \"ResolvedAt\" IS NOT NULL AND \"ResolvedByUserId\" IS NOT NULL AND ((\"Status\" = 'Approved' AND \"ReplacementUserId\" IS NOT NULL) OR (\"Status\" <> 'Approved' AND \"ReplacementUserId\" IS NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Coverage_Status",
                table: "CoverageRequests",
                sql: "\"Status\" IN ('Pending','Approved','Rejected','Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_ActorUserId",
                table: "AuditEntries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_OrganizationId_Action_CreatedAt",
                table: "AuditEntries",
                columns: new[] { "OrganizationId", "Action", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SimulationWorkspaces_OwnerUserId",
                table: "SimulationWorkspaces",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SimulationWorkspaces_SourceOrganizationId_OwnerUserId",
                table: "SimulationWorkspaces",
                columns: new[] { "SourceOrganizationId", "OwnerUserId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CoverageRequests_AspNetUsers_ResolvedByUserId",
                table: "CoverageRequests",
                column: "ResolvedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CoverageRequests_Memberships_OrganizationId_ReplacementUser~",
                table: "CoverageRequests",
                columns: new[] { "OrganizationId", "ReplacementUserId" },
                principalTable: "Memberships",
                principalColumns: new[] { "OrganizationId", "UserId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CoverageRequests_AspNetUsers_ResolvedByUserId",
                table: "CoverageRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_CoverageRequests_Memberships_OrganizationId_ReplacementUser~",
                table: "CoverageRequests");

            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "SimulationWorkspaces");

            migrationBuilder.DropIndex(
                name: "IX_CoverageRequests_OrganizationId_ReplacementUserId",
                table: "CoverageRequests");

            migrationBuilder.DropIndex(
                name: "IX_CoverageRequests_ResolvedByUserId",
                table: "CoverageRequests");

            migrationBuilder.DropIndex(
                name: "IX_CoverageRequests_ShiftId",
                table: "CoverageRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Coverage_Kind",
                table: "CoverageRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Coverage_Resolution",
                table: "CoverageRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Coverage_Status",
                table: "CoverageRequests");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "CoverageRequests");

            migrationBuilder.DropColumn(
                name: "ReplacementUserId",
                table: "CoverageRequests");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "CoverageRequests");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserId",
                table: "CoverageRequests");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "CoverageRequests");

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRequests_ShiftId",
                table: "CoverageRequests",
                column: "ShiftId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Coverage_Status",
                table: "CoverageRequests",
                sql: "\"Status\" = 'Pending'");
        }
    }
}
