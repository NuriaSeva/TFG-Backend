using FindMind.Data;
using FindMind.DTO.Banking;
using FindMind.DTOs.Banking;
using FindMind.Interfaces;
using FindMind.Models.Enitdades;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FindMind.Services;

public class CategoriaSeedService : ICategoriaSeedService
{
    private readonly HttpClient _httpClient;
    private readonly FindMindDbContext _context;

    public CategoriaSeedService(HttpClient httpClient, FindMindDbContext context)
    {
        _httpClient = httpClient;
        _context = context;
    }

    public async Task<int> ImportarCategoriasDesdeTinkAsync(string locale = "es_ES")
    {
        var url = $"https://api.tink.com/api/v1/categories?locale={Uri.EscapeDataString(locale)}";

        using var response = await _httpClient.GetAsync(url);
        var rawJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Error obteniendo categorías de Tink. Status: {(int)response.StatusCode}. Body: {rawJson}");
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var tinkCategories = JsonSerializer.Deserialize<List<TinkCategoriaDto>>(rawJson, options);

        if (tinkCategories == null || tinkCategories.Count == 0)
        {
            return 0;
        }

        var insertadas = 0;

        foreach (var item in tinkCategories)
        {
            // Ignoramos transferencias para no complicar el modelo
            if (!EsTipoImportable(item.Type))
                continue;

            var nombre = ObtenerNombreCategoria(item);

            if (string.IsNullOrWhiteSpace(nombre))
                continue;

            var tipoCategoria = MapearTipoCategoria(item.Type);

            // Evita duplicados entre categorías del sistema
            var existe = await _context.Categorias.AnyAsync(c =>
                c.UsuarioId == null &&
                c.EsSistema &&
                c.Nombre == nombre);

            if (existe)
                continue;

            var categoria = new Categoria
            {
                Id = Guid.NewGuid(),
                UsuarioId = null,
                Nombre = nombre,
                Tipo = tipoCategoria,
                EsSistema = true,
                Archivada = false,
                FechaCreacion = DateTime.UtcNow
            };

            _context.Categorias.Add(categoria);
            insertadas++;
        }

        if (insertadas > 0)
        {
            await _context.SaveChangesAsync();
        }

        return insertadas;
    }

    private static bool EsTipoImportable(string type)
    {
        return type == "EXPENSES" || type == "INCOME";
    }

    private static TipoCategoria MapearTipoCategoria(string type)
    {
        return type switch
        {
            "INCOME" => TipoCategoria.Ingreso,
            _ => TipoCategoria.Gasto
        };
    }

    private static string ObtenerNombreCategoria(TinkCategoriaDto item)
    {
        var nombreBase = !string.IsNullOrWhiteSpace(item.SecondaryName)
            ? item.SecondaryName!
            : item.PrimaryName;

        return TraducirCategoria(nombreBase);
    }

    private static string TraducirCategoria(string nombre)
    {
        return nombre.Trim() switch
        {
            "Expenses" => "Gastos",
            "Income" => "Ingresos",
            "Household & Services" => "Hogar y servicios",
            "Rent" => "Alquiler",
            "Mortgage & Interest" => "Hipoteca e intereses",
            "Media & IT" => "Internet y tecnología",
            "Utilities" => "Suministros",
            "Insurance & Fees" => "Seguros y comisiones",
            "Services" => "Servicios",
            "Home Improvements" => "Mejoras del hogar",
            "Renovation & Repairs" => "Reformas y reparaciones",
            "Furniture & Interior" => "Muebles e interior",
            "Garden" => "Jardín",
            "Food & Drinks" => "Comida y bebida",
            "Groceries" => "Supermercado",
            "Restaurants" => "Restaurantes",
            "Coffee & Snacks" => "Café y snacks",
            "Alcohol & Tobacco" => "Alcohol y tabaco",
            "Bars" => "Bares",
            "Transport" => "Transporte",
            "Car & Fuel" => "Coche y combustible",
            "Public Transport" => "Transporte público",
            "Flights" => "Vuelos",
            "Taxi" => "Taxi",
            "Shopping" => "Compras",
            "Clothes & Accessories" => "Ropa y accesorios",
            "Electronics" => "Electrónica",
            "Hobby & Sports Equipment" => "Hobby y deporte",
            "Books & Games" => "Libros y juegos",
            "Gifts" => "Regalos",
            "Leisure" => "Ocio",
            "Culture & Events" => "Cultura y eventos",
            "Hobbies" => "Aficiones",
            "Sports & Fitness" => "Deporte y fitness",
            "Vacation" => "Vacaciones",
            "Health & Beauty" => "Salud y belleza",
            "Healthcare" => "Salud",
            "Pharmacy" => "Farmacia",
            "Eyecare" => "Óptica",
            "Beauty" => "Belleza",
            "Other" => "Otros",
            "Cash Withdrawals" => "Retiradas de efectivo",
            "Business Expenses" => "Gastos profesionales",
            "Kids" => "Niños",
            "Pets" => "Mascotas",
            "Charity" => "Donaciones",
            "Education" => "Educación",
            "Uncategorized" => "Sin categorizar",
            "Salary" => "Nómina",
            "Pension" => "Pensión",
            "Reimbursements" => "Reembolsos",
            "Benefits" => "Prestaciones",
            "Financial" => "Financiero",
            "Other Income" => "Otros ingresos",
            _ => nombre.Trim()
        };
    }
}


