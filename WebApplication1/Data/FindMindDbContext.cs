using FindMind.Models.Enitdades;
using Microsoft.EntityFrameworkCore;

namespace FindMind.Data;

public class FindMindDbContext : DbContext
{
    public FindMindDbContext(DbContextOptions<FindMindDbContext> options)
        : base(options)
    {
    }

    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<ConfiguracionUsuario> ConfiguracionesUsuario { get; set; }
    public DbSet<ConexionBancaria> ConexionesBancarias { get; set; }
    public DbSet<Alerta> Alertas { get; set; }
    public DbSet<Exportacion> Exportaciones { get; set; }
    public DbSet<Categoria> Categorias { get; set; }
    public DbSet<CuentaBancaria> CuentasBancarias { get; set; }
    public DbSet<SincronizacionBancaria> SincronizacionesBancarias { get; set; }
    public DbSet<Transaccion> Transacciones { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigurarUsuario(modelBuilder);
        ConfigurarConfiguracionUsuario(modelBuilder);
        ConfigurarConexionBancaria(modelBuilder);
        ConfigurarAlerta(modelBuilder);
        ConfigurarExportacion(modelBuilder);
        ConfigurarCategoria(modelBuilder);
        ConfigurarCuentaBancaria(modelBuilder);
        ConfigurarSincronizacionBancaria(modelBuilder);
        ConfigurarTransaccion(modelBuilder);
    }

    private static void ConfigurarUsuario(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("usuarios");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Nombre)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Apellidos)
                .HasMaxLength(150);

            entity.Property(e => e.MonedaPreferida)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(e => e.Idioma)
                .IsRequired()
                .HasMaxLength(10);

            entity.HasIndex(e => e.Email)
                .IsUnique();
        });
    }

    private static void ConfigurarConfiguracionUsuario(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConfiguracionUsuario>(entity =>
        {
            entity.ToTable("configuraciones_usuario");

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Usuario)
                .WithOne(u => u.ConfiguracionUsuario)
                .HasForeignKey<ConfiguracionUsuario>(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UsuarioId)
                .IsUnique();
        });
    }

    private static void ConfigurarConexionBancaria(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConexionBancaria>(entity =>
        {
            entity.ToTable("conexiones_bancarias");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.IdConexionExterna)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(e => e.AccessToken)
                .HasColumnType("longtext");

            entity.Property(e => e.RefreshToken)
                .HasColumnType("longtext");

            entity.HasOne(e => e.Usuario)
                .WithMany(u => u.ConexionesBancarias)
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurarAlerta(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Alerta>(entity =>
        {
            entity.ToTable("alertas");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Titulo)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Mensaje)
                .IsRequired()
                .HasMaxLength(1000);

            entity.HasOne(e => e.Usuario)
                .WithMany(u => u.Alertas)
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurarExportacion(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Exportacion>(entity =>
        {
            entity.ToTable("exportaciones");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.RutaArchivo)
                .HasMaxLength(500);

            entity.HasOne(e => e.Usuario)
                .WithMany(u => u.Exportaciones)
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurarCategoria(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.ToTable("categorias");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Nombre)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Color)
                .HasMaxLength(20);

            entity.Property(e => e.Icono)
                .HasMaxLength(50);

            entity.HasOne(e => e.Usuario)
                .WithMany(u => u.Categorias)
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.UsuarioId, e.Nombre, e.Tipo });
        });
    }

    private static void ConfigurarCuentaBancaria(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CuentaBancaria>(entity =>
        {
            entity.ToTable("cuentas_bancarias");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.IdCuentaExterna)
                .HasMaxLength(150);

            entity.Property(e => e.Nombre)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(e => e.Iban)
                .HasMaxLength(34);

            entity.Property(e => e.Banco)
                .HasMaxLength(120);

            entity.Property(e => e.BIC)
                .HasMaxLength(20);

            entity.Property(e => e.Moneda)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(e => e.Tipo)
                .HasMaxLength(50);

            entity.HasOne(e => e.Usuario)
                .WithOne(u => u.CuentaBancaria)
                .HasForeignKey<CuentaBancaria>(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UsuarioId)
                .IsUnique();

            entity.HasOne(e => e.ConexionBancaria)
                .WithOne(c => c.CuentaBancaria)
                .HasForeignKey<CuentaBancaria>(e => e.ConexionBancariaId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.ConexionBancariaId)
                .IsUnique();
        });
    }

    private static void ConfigurarSincronizacionBancaria(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SincronizacionBancaria>(entity =>
        {
            entity.ToTable("sincronizaciones_bancarias");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.MensajeError)
                .HasMaxLength(1000);

            entity.HasOne(e => e.ConexionBancaria)
                .WithMany(c => c.Sincronizaciones)
                .HasForeignKey(e => e.ConexionBancariaId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurarTransaccion(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaccion>(entity =>
        {
            entity.ToTable("transacciones");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Importe)
                .HasPrecision(18, 2);

            entity.Property(e => e.Moneda)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(e => e.Descripcion)
                .HasMaxLength(500);

            entity.Property(e => e.IdTransaccionExterna)
                .HasMaxLength(150);

            entity.HasOne(e => e.Usuario)
                .WithMany(u => u.Transacciones)
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CuentaBancaria)
                .WithMany(c => c.Transacciones)
                .HasForeignKey(e => e.CuentaBancariaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Categoria)
                .WithMany(c => c.Transacciones)
                .HasForeignKey(e => e.CategoriaId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.UsuarioId, e.Fecha });

            entity.HasIndex(e => new { e.UsuarioId, e.Origen, e.IdTransaccionExterna });
        });
    }
}