using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinMind.Migrations
{
    /// <inheritdoc />
    public partial class AjustarCategoriasTipo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                table: "categorias",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "categorias");
        }
    }
}
