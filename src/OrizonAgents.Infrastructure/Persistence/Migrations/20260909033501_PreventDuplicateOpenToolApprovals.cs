using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrizonAgents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreventDuplicateOpenToolApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ToolExecutionApprovals_OpenEquivalentRequest",
                table: "ToolExecutionApprovals",
                columns: new[] { "TenantId", "AgentId", "ToolId", "InputHash" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Approved')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ToolExecutionApprovals_OpenEquivalentRequest",
                table: "ToolExecutionApprovals");
        }
    }
}
