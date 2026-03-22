namespace FinMind.DTO;

public class SeleccionarCuentaRequestDto
{
    public Guid UsuarioId { get; set; }
    public Guid ConexionBancariaId { get; set; }
    public string TinkAccountId { get; set; } = string.Empty;
}