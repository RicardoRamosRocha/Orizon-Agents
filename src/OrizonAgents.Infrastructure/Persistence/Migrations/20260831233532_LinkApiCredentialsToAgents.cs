using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrizonAgents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkApiCredentialsToAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApiCredentials_TenantId_IsActive",
                table: "ApiCredentials");

            migrationBuilder.AddColumn<Guid>(
                name: "AgentId",
                table: "ApiCredentials",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KeyIdentifier",
                table: "ApiCredentials",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAtUtc",
                table: "ApiCredentials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "ApiCredentials"
                SET "IsActive" = FALSE,
                    "RevokedAtUtc" = COALESCE("UpdatedAtUtc", "CreatedAtUtc")
                WHERE "AgentId" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ApiCredentials_AgentId",
                table: "ApiCredentials",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiCredentials_KeyIdentifier",
                table: "ApiCredentials",
                column: "KeyIdentifier",
                unique: true,
                filter: "\"KeyIdentifier\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ApiCredentials_TenantId_AgentId",
                table: "ApiCredentials",
                columns: new[] { "TenantId", "AgentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ApiCredentials_AiAgents_AgentId",
                table: "ApiCredentials",
                column: "AgentId",
                principalTable: "AiAgents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiCredentials_AiAgents_AgentId",
                table: "ApiCredentials");

            migrationBuilder.DropIndex(
                name: "IX_ApiCredentials_AgentId",
                table: "ApiCredentials");

            migrationBuilder.DropIndex(
                name: "IX_ApiCredentials_KeyIdentifier",
                table: "ApiCredentials");

            migrationBuilder.DropIndex(
                name: "IX_ApiCredentials_TenantId_AgentId",
                table: "ApiCredentials");

            migrationBuilder.DropColumn(
                name: "AgentId",
                table: "ApiCredentials");

            migrationBuilder.DropColumn(
                name: "KeyIdentifier",
                table: "ApiCredentials");

            migrationBuilder.DropColumn(
                name: "RevokedAtUtc",
                table: "ApiCredentials");

            migrationBuilder.CreateIndex(
                name: "IX_ApiCredentials_TenantId_IsActive",
                table: "ApiCredentials",
                columns: new[] { "TenantId", "IsActive" });
        }
    }
}
