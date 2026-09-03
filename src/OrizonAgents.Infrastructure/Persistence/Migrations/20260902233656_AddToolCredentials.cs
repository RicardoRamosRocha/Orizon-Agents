using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrizonAgents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddToolCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ToolCredentialId",
                table: "AgentTools",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ToolCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AuthenticationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    HeaderName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EncryptedSecret = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolCredentials", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentTools_ToolCredentialId",
                table: "AgentTools",
                column: "ToolCredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolCredentials_TenantId_IsActive",
                table: "ToolCredentials",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ToolCredentials_TenantId_Name",
                table: "ToolCredentials",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AgentTools_ToolCredentials_ToolCredentialId",
                table: "AgentTools",
                column: "ToolCredentialId",
                principalTable: "ToolCredentials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentTools_ToolCredentials_ToolCredentialId",
                table: "AgentTools");

            migrationBuilder.DropTable(
                name: "ToolCredentials");

            migrationBuilder.DropIndex(
                name: "IX_AgentTools_ToolCredentialId",
                table: "AgentTools");

            migrationBuilder.DropColumn(
                name: "ToolCredentialId",
                table: "AgentTools");
        }
    }
}
