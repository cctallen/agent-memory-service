using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentMemoryService.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "permissions",
                table: "agent_namespaces",
                type: "integer",
                nullable: false,
                defaultValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "permissions",
                table: "agent_namespaces");
        }
    }
}
