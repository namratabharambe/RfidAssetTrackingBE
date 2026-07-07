using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSiteAndSiteType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SiteId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SiteType",
                table: "Sites",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_SiteId",
                table: "Users",
                column: "SiteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Sites_SiteId",
                table: "Users",
                column: "SiteId",
                principalTable: "Sites",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Sites_SiteId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_SiteId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SiteType",
                table: "Sites");
        }
    }
}
