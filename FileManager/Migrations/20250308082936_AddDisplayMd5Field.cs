using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileManager.Migrations
{
    /// <inheritdoc />
    public partial class AddDisplayMd5Field : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayMd5",
                table: "FileItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayMd5",
                table: "FileItems");
        }
    }
}
