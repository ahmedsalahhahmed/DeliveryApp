using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryApp.Migrations
{
    /// <inheritdoc />
    public partial class AddRatingsSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DriverRating",
                table: "Rides",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriverReview",
                table: "Rides",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DriverRating",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "DriverReview",
                table: "Rides");
        }
    }
}
