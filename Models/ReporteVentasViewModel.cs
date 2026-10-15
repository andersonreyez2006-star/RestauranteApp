namespace RestauranteApp.Models;

// Agrupa los indicadores del reporte diario para que la vista no consulte la base de datos.
public sealed class ReporteVentasViewModel
{
    public DateTime Fecha { get; init; }
    public int CantidadFacturas { get; init; }
    public decimal TotalVentas { get; init; }
    public List<(string Nombre, int Unidades)> MasVendidos { get; init; } = new();
    public List<Platillo> BajoStock { get; init; } = new();
}
