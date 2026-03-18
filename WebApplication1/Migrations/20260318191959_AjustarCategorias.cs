using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FindMind.Migrations
{
    /// <inheritdoc />
    public partial class AjustarCategorias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "categorias");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                table: "categorias",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
