using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrizonAgents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddToolExecutionApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ToolExecutionApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolExecutionApprovals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ToolExecutionApprovals_ExpiresAtUtc",
                table: "ToolExecutionApprovals",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ToolExecutionApprovals_TenantId_AgentId_ToolId_InputHash_St~",
                table: "ToolExecutionApprovals",
                columns: new[] { "TenantId", "AgentId", "ToolId", "InputHash", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ToolExecutionApprovals");
        }
    }
}
