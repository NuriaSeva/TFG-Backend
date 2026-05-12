namespace FinMind.DTO.Admin;

public class AdminHealthResponseDto
{
    public string EstadoApi { get; set; } = "healthy";
    public DateTime TimestampUtc { get; set; }
    public string Entorno { get; set; } = null!;
    public AdminDatabaseStatusDto BaseDeDatos { get; set; } = new();
    public AdminStorageStatusDto Almacenamiento { get; set; } = new();
    public AdminModeloPrediccionGastoDto ModeloPrediccionGasto { get; set; } = new();
}

public class AdminDatabaseStatusDto
{
    public bool Conectada { get; set; }
    public string Proveedor { get; set; } = null!;
    public long TotalUsuarios { get; set; }
    public long? TamanoBytes { get; set; }
}

public class AdminStorageStatusDto
{
    public string Unidad { get; set; } = null!;
    public long TotalBytes { get; set; }
    public long DisponibleBytes { get; set; }
    public decimal PorcentajeLibre { get; set; }
}

public class AdminModeloPrediccionGastoDto
{
    public bool DatasetDisponible { get; set; }
    public bool ModeloDisponible { get; set; }
    public int RegistrosDataset { get; set; }
    public decimal? Mae { get; set; }
    public decimal? Rmse { get; set; }
    public decimal? R2 { get; set; }
    public DateTime? FechaModeloUtc { get; set; }
    public string? Mensaje { get; set; }
}
