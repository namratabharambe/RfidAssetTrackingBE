using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetMasterFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssetType",
                table: "Assets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessUnit",
                table: "Assets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentCustodian",
                table: "Assets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustodianEmail",
                table: "Assets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "Assets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Industry",
                table: "Assets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "Assets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerDepartment",
                table: "Assets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrCode",
                table: "Assets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarrantyProvider",
                table: "Assets",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssetType",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "BusinessUnit",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CurrentCustodian",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CustodianEmail",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Group",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Industry",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "OwnerDepartment",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "QrCode",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "WarrantyProvider",
                table: "Assets");
        }
    }
}
