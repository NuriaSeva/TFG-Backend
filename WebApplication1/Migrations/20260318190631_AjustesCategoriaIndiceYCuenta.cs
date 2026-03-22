using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinMind.Migrations
{
    /// <inheritdoc />
    public partial class AjustesCategoriaIndiceYCuenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_categorias_UsuarioId",
                table: "categorias",
                column: "UsuarioId");

            migrationBuilder.DropIndex(
                name: "IX_categorias_UsuarioId_Nombre_Tipo",
                table: "categorias");

            migrationBuilder.AlterColumn<Guid>(
                name: "CuentaBancariaId",
                table: "transacciones",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "Proveedor",
                table: "transacciones",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaUltimaSincronizacion",
                table: "cuentas_bancarias",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SaldoActual",
                table: "cuentas_bancarias",
                type: "decimal(65,30)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_categorias_UsuarioId_Nombre",
                table: "categorias",
                columns: new[] { "UsuarioId", "Nombre" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_categorias_UsuarioId_Nombre",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "Proveedor",
                table: "transacciones");

            migrationBuilder.DropColumn(
                name: "FechaUltimaSincronizacion",
                table: "cuentas_bancarias");

            migrationBuilder.DropColumn(
                name: "SaldoActual",
                table: "cuentas_bancarias");

            migrationBuilder.AlterColumn<Guid>(
                name: "CuentaBancariaId",
                table: "transacciones",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_categorias_UsuarioId_Nombre_Tipo",
                table: "categorias",
                columns: new[] { "UsuarioId", "Nombre", "Tipo" });

            migrationBuilder.DropIndex(
                name: "IX_categorias_UsuarioId",
                table: "categorias");
        }
    }
}
