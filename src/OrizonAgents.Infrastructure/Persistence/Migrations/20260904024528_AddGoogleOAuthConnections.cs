using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrizonAgents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleOAuthConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyStamp",
                table: "IntegrationConnections",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConnectedAccountEmail",
                table: "IntegrationConnections",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingOAuthStateHash",
                table: "IntegrationConnections",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "IntegrationConnections");

            migrationBuilder.DropColumn(
                name: "ConnectedAccountEmail",
                table: "IntegrationConnections");

            migrationBuilder.DropColumn(
                name: "PendingOAuthStateHash",
                table: "IntegrationConnections");
        }
    }
}
