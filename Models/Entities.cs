using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace RestauranteApp.Models;

public class Usuario {
    [Key] public int IdUsuario { get; set; }
    [Required, StringLength(100)] public string Nombre { get; set; } = "";
    [Required, EmailAddress, StringLength(150)] public string Correo { get; set; } = "";
    [Required, StringLength(255)] public string Contrasena { get; set; } = ""; // guardar HASH, nunca texto plano
    [Required, StringLength(50)] public string Rol { get; set; } = "Empleado";
}
public class Mesa {
    [Key] public int IdMesa { get; set; }
    [Range(1, 999)] public int NumeroMesa { get; set; }
    [Range(1, 50)] public int Capacidad { get; set; }
    [Required, StringLength(30)] public string Estado { get; set; } = "Libre";
}
public class Categoria {
    [Key] public int IdCategoria { get; set; }
    [Required, StringLength(100)] public string Nombre { get; set; } = "";
    public List<Platillo> Platillos { get; set; } = new();
}
public class Platillo {
    [Key] public int IdPlatillo { get; set; }
    [Required(ErrorMessage = "El nombre es obligatorio"), StringLength(150)] public string Nombre { get; set; } = "";
    [StringLength(300)] public string? Descripcion { get; set; }
    [Range(0.01, 10000, ErrorMessage = "Precio inválido"), Column(TypeName = "decimal(10,2)")] public decimal Precio { get; set; }
    [Required, StringLength(30)] public string Estado { get; set; } = "Disponible";
    [Range(0, 100000)] public int Existencias { get; set; } = 0;
    public int IdCategoria { get; set; }
    public Categoria? Categoria { get; set; }
}
public class Orden {
    [Key] public int IdOrden { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    [Required, StringLength(30)] public string Estado { get; set; } = "Pendiente";
    public int IdMesa { get; set; }
    public Mesa? Mesa { get; set; }
    public int? IdUsuario { get; set; }
    public Usuario? Usuario { get; set; }
    public List<DetalleOrden> Detalles { get; set; } = new();
}
public class DetalleOrden {
    [Key] public int IdDetalle { get; set; }
    [Range(1, 100)] public int Cantidad { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal PrecioUnitario { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal Subtotal { get; set; }
    public int IdOrden { get; set; }
    public Orden? Orden { get; set; }
    public int IdPlatillo { get; set; }
    public Platillo? Platillo { get; set; }
}
public class Factura {
    [Key] public int IdFactura { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    [Column(TypeName = "decimal(10,2)")] public decimal Subtotal { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal IVA { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal Total { get; set; }
    public int IdOrden { get; set; }
    public Orden? Orden { get; set; }
    public List<Pago> Pagos { get; set; } = new();
}
public class Pago {
    [Key] public int IdPago { get; set; }
    [Required, StringLength(50)] public string MetodoPago { get; set; } = "Efectivo";
    [Column(TypeName = "decimal(10,2)")] public decimal Monto { get; set; }
    public DateTime FechaPago { get; set; } = DateTime.Now;
    public int IdFactura { get; set; }
    public Factura? Factura { get; set; }
}
