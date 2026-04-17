using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinMind.Migrations
{
    /// <inheritdoc />
    public partial class MigracionDigitalOcean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transacciones_UsuarioId_Origen_IdTransaccionExterna",
                table: "transacciones");

            migrationBuilder.DropIndex(
                name: "IX_categorias_UsuarioId_Nombre",
                table: "categorias");

            migrationBuilder.CreateIndex(
                name: "IX_transacciones_UsuarioId_Proveedor_IdTransaccionExterna",
                table: "transacciones",
                columns: new[] { "UsuarioId", "Proveedor", "IdTransaccionExterna" });

            migrationBuilder.CreateIndex(
                name: "IX_categorias_EsSistema_Nombre_Tipo",
                table: "categorias",
                columns: new[] { "EsSistema", "Nombre", "Tipo" });

            migrationBuilder.CreateIndex(
                name: "IX_categorias_UsuarioId_Nombre_Tipo",
                table: "categorias",
                columns: new[] { "UsuarioId", "Nombre", "Tipo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transacciones_UsuarioId_Proveedor_IdTransaccionExterna",
                table: "transacciones");

            migrationBuilder.DropIndex(
                name: "IX_categorias_EsSistema_Nombre_Tipo",
                table: "categorias");

            migrationBuilder.DropIndex(
                name: "IX_categorias_UsuarioId_Nombre_Tipo",
                table: "categorias");

            migrationBuilder.CreateIndex(
                name: "IX_transacciones_UsuarioId_Origen_IdTransaccionExterna",
                table: "transacciones",
                columns: new[] { "UsuarioId", "Origen", "IdTransaccionExterna" });

            migrationBuilder.CreateIndex(
                name: "IX_categorias_UsuarioId_Nombre",
                table: "categorias",
                columns: new[] { "UsuarioId", "Nombre" },
                unique: true);
        }
    }
}
