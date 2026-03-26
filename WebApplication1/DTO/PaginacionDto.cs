namespace FinMind.DTO;

public class PaginacionDTO<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int Tamanyo { get; set; }
    public int TotalPaginas { get; set; }
}