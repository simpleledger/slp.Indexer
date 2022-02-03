using Microsoft.EntityFrameworkCore.Migrations;

namespace Slp.Migrations.POSTGRESQL.Migrations
{
    public partial class RemoveAddressBlockHeightFromDb : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SlpAddress_SlpBlock_BlockHeight",
                table: "SlpAddress");

            migrationBuilder.DropIndex(
                name: "IX_SlpAddress_BlockHeight",
                table: "SlpAddress");

            migrationBuilder.DropColumn(
                name: "BlockHeight",
                table: "SlpAddress");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BlockHeight",
                table: "SlpAddress",
                type: "integer",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlpAddress_BlockHeight",
                table: "SlpAddress",
                column: "BlockHeight");

            migrationBuilder.AddForeignKey(
                name: "FK_SlpAddress_SlpBlock_BlockHeight",
                table: "SlpAddress",
                column: "BlockHeight",
                principalTable: "SlpBlock",
                principalColumn: "Height",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
