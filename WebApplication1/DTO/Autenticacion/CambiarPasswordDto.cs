namespace FinMind.DTO.Autenticacion;

public class CambiarPasswordDto
{
    public string PasswordActual { get; set; } = string.Empty;

    public string PasswordNueva { get; set; } = string.Empty;
}
