using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PdfToolStack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentMemoryConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "UserMemorySettings",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "DocumentMemories",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "DocumentMemories",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "DocumentType",
                table: "DocumentMemories",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_UserMemorySettings_UserId",
                table: "UserMemorySettings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMemories_ExpiresAt",
                table: "DocumentMemories",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMemories_UserId",
                table: "DocumentMemories",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserMemorySettings_UserId",
                table: "UserMemorySettings");

            migrationBuilder.DropIndex(
                name: "IX_DocumentMemories_ExpiresAt",
                table: "DocumentMemories");

            migrationBuilder.DropIndex(
                name: "IX_DocumentMemories_UserId",
                table: "DocumentMemories");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "UserMemorySettings",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "DocumentMemories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "DocumentMemories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);

            migrationBuilder.AlterColumn<string>(
                name: "DocumentType",
                table: "DocumentMemories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);
        }
    }
}
