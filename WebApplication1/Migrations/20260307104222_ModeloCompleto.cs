using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinMind.Migrations
{
    /// <inheritdoc />
    public partial class ModeloCompleto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transacciones_cuentas_CuentaId",
                table: "transacciones");

            migrationBuilder.DropTable(
                name: "cuentas");

            migrationBuilder.DropIndex(
                name: "IX_transacciones_Fecha",
                table: "transacciones");

            migrationBuilder.DropIndex(
                name: "IX_transacciones_UsuarioId",
                table: "transacciones");

            migrationBuilder.DropIndex(
                name: "IX_transacciones_UsuarioId_Tipo_Fecha",
                table: "transacciones");

            migrationBuilder.DropIndex(
                name: "IX_categorias_EsSistema_Tipo",
                table: "categorias");

            migrationBuilder.DropIndex(
                name: "IX_categorias_UsuarioId",
                table: "categorias");

            migrationBuilder.RenameColumn(
                name: "CuentaId",
                table: "transacciones",
                newName: "CuentaBancariaId");

            migrationBuilder.RenameIndex(
                name: "IX_transacciones_CuentaId",
                table: "transacciones",
                newName: "IX_transacciones_CuentaBancariaId");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCambioPassword",
                table: "usuarios",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaUltimoAcceso",
                table: "usuarios",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "alertas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UsuarioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Mensaje = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Leida = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alertas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alertas_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "conexiones_bancarias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UsuarioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Proveedor = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    IdConexionExterna = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AccessToken = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RefreshToken = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FechaExpiracionToken = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FechaUltimaSincronizacion = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conexiones_bancarias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_conexiones_bancarias_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "configuraciones_usuario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UsuarioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TamanoTexto = table.Column<int>(type: "int", nullable: false),
                    NotificacionesActivas = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FormatoExportacionPreferido = table.Column<int>(type: "int", nullable: false),
                    Tema = table.Column<int>(type: "int", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuraciones_usuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_configuraciones_usuario_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "exportaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UsuarioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Formato = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    RutaArchivo = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FechaCreacion = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exportaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exportaciones_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cuentas_bancarias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UsuarioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ConexionBancariaId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    IdCuentaExterna = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nombre = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Iban = table.Column<string>(type: "varchar(34)", maxLength: 34, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Banco = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BIC = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Moneda = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Tipo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Activa = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cuentas_bancarias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cuentas_bancarias_conexiones_bancarias_ConexionBancariaId",
                        column: x => x.ConexionBancariaId,
                        principalTable: "conexiones_bancarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cuentas_bancarias_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sincronizaciones_bancarias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ConexionBancariaId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FechaInicio = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    MensajeError = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NumeroMovimientosImportados = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sincronizaciones_bancarias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sincronizaciones_bancarias_conexiones_bancarias_ConexionBanc~",
                        column: x => x.ConexionBancariaId,
                        principalTable: "conexiones_bancarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_alertas_UsuarioId",
                table: "alertas",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_conexiones_bancarias_UsuarioId",
                table: "conexiones_bancarias",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_configuraciones_usuario_UsuarioId",
                table: "configuraciones_usuario",
                column: "UsuarioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cuentas_bancarias_ConexionBancariaId",
                table: "cuentas_bancarias",
                column: "ConexionBancariaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cuentas_bancarias_UsuarioId",
                table: "cuentas_bancarias",
                column: "UsuarioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exportaciones_UsuarioId",
                table: "exportaciones",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_sincronizaciones_bancarias_ConexionBancariaId",
                table: "sincronizaciones_bancarias",
                column: "ConexionBancariaId");

            migrationBuilder.AddForeignKey(
                name: "FK_transacciones_cuentas_bancarias_CuentaBancariaId",
                table: "transacciones",
                column: "CuentaBancariaId",
                principalTable: "cuentas_bancarias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transacciones_cuentas_bancarias_CuentaBancariaId",
                table: "transacciones");

            migrationBuilder.DropTable(
                name: "alertas");

            migrationBuilder.DropTable(
                name: "configuraciones_usuario");

            migrationBuilder.DropTable(
                name: "cuentas_bancarias");

            migrationBuilder.DropTable(
                name: "exportaciones");

            migrationBuilder.DropTable(
                name: "sincronizaciones_bancarias");

            migrationBuilder.DropTable(
                name: "conexiones_bancarias");

            migrationBuilder.DropColumn(
                name: "FechaCambioPassword",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "FechaUltimoAcceso",
                table: "usuarios");

            migrationBuilder.RenameColumn(
                name: "CuentaBancariaId",
                table: "transacciones",
                newName: "CuentaId");

            migrationBuilder.RenameIndex(
                name: "IX_transacciones_CuentaBancariaId",
                table: "transacciones",
                newName: "IX_transacciones_CuentaId");

            migrationBuilder.CreateTable(
                name: "cuentas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UsuarioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Archivada = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    BIC = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Banco = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FechaCreacion = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Iban = table.Column<string>(type: "varchar(34)", maxLength: 34, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Moneda = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Tipo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cuentas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cuentas_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_transacciones_Fecha",
                table: "transacciones",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_transacciones_UsuarioId",
                table: "transacciones",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_transacciones_UsuarioId_Tipo_Fecha",
                table: "transacciones",
                columns: new[] { "UsuarioId", "Tipo", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_categorias_EsSistema_Tipo",
                table: "categorias",
                columns: new[] { "EsSistema", "Tipo" });

            migrationBuilder.CreateIndex(
                name: "IX_categorias_UsuarioId",
                table: "categorias",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_cuentas_UsuarioId",
                table: "cuentas",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_cuentas_UsuarioId_Nombre",
                table: "cuentas",
                columns: new[] { "UsuarioId", "Nombre" });

            migrationBuilder.AddForeignKey(
                name: "FK_transacciones_cuentas_CuentaId",
                table: "transacciones",
                column: "CuentaId",
                principalTable: "cuentas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
