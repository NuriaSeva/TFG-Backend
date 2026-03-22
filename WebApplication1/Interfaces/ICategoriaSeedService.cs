namespace FinMind.Interfaces
{
    public interface ICategoriaSeedService
    {

        Task<int> ImportarCategoriasDesdeTinkAsync(string locale = "es_ES");
    }

}
