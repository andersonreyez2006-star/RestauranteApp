namespace RestauranteApp.Models;

// Modelo de presentación, no entidad persistida: el controlador calcula sus indicadores
// y la vista los muestra sin ejecutar consultas ni aplicar reglas de negocio.
public sealed class ReporteVentasViewModel
{
    // Día seleccionado en el filtro; representa el día completo del reporte.
    public DateTime Fecha { get; init; }
    // Número de facturas emitidas dentro del día seleccionado.
    public int CantidadFacturas { get; init; }
    // Suma de totales facturados, incluido el IVA.
    public decimal TotalVentas { get; init; }
    // Hasta cinco platillos con más unidades vendidas ese día, ordenados de mayor a menor.
    public List<(string Nombre, int Unidades)> MasVendidos { get; init; } = new();
    // Platillos cuyo stock actual es menor o igual al umbral operativo de cinco unidades.
    public List<Platillo> BajoStock { get; init; } = new();
}
