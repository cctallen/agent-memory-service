using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentMemoryService.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_memories_namespaces_NamespaceId",
                table: "memories",
                column: "NamespaceId",
                principalTable: "namespaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_memory_access_logs_memories_MemoryId",
                table: "memory_access_logs",
                column: "MemoryId",
                principalTable: "memories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_memory_access_logs_namespaces_NamespaceId",
                table: "memory_access_logs",
                column: "NamespaceId",
                principalTable: "namespaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_secrets_namespaces_NamespaceId",
                table: "secrets",
                column: "NamespaceId",
                principalTable: "namespaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_memories_namespaces_NamespaceId",
                table: "memories");

            migrationBuilder.DropForeignKey(
                name: "FK_memory_access_logs_memories_MemoryId",
                table: "memory_access_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_memory_access_logs_namespaces_NamespaceId",
                table: "memory_access_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_secrets_namespaces_NamespaceId",
                table: "secrets");
        }
    }
}
