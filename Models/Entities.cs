using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace RestauranteApp.Models;

// Usuario almacena la identidad del personal. El rol se convierte en un claim al iniciar sesión
// y luego se usa en [Authorize]; Contrasena debe contener siempre el hash generado por Identity.
public class Usuario {
    [Key] public int IdUsuario { get; set; }
    // Nombre que se muestra en la interfaz y en las órdenes creadas por personal.
    [Required, StringLength(100)] public string Nombre { get; set; } = "";
    // Correo usado para localizar la cuenta al iniciar sesión; no se usa como contraseña ni como rol.
    [Required, EmailAddress, StringLength(150)] public string Correo { get; set; } = "";
    // Hash de contraseña, no texto original; el verificador de ASP.NET Core Identity comprueba el inicio.
    [Required, StringLength(255)] public string Contrasena { get; set; } = "";
    // Valor esperado por las políticas de autorización, por ejemplo Administrador, Mesero, Cocina o Cajero.
    [Required, StringLength(50)] public string Rol { get; set; } = "Empleado";
}

// Mesa identifica un sitio físico del restaurante y su disponibilidad para asignar órdenes.
public class Mesa {
    [Key] public int IdMesa { get; set; }
    // Número visible para el cliente y el personal; el controlador impide repetirlo.
    [Range(1, 999)] public int NumeroMesa { get; set; }
    // Cantidad máxima de personas que pueden ocupar la mesa.
    [Range(1, 50)] public int Capacidad { get; set; }
    // Estado operativo: normalmente Libre u Ocupada; se actualiza al abrir y facturar una orden.
    [Required, StringLength(30)] public string Estado { get; set; } = "Libre";
}

// Categoria organiza el menú; Platillos es la navegación inversa que EF Core carga cuando hace falta.
public class Categoria {
    [Key] public int IdCategoria { get; set; }
    // Nombre mostrado en el menú; el controlador valida longitud y evita duplicados.
    [Required, StringLength(100)] public string Nombre { get; set; } = "";
    // No es una columna independiente: representa los platillos cuya FK IdCategoria apunta aquí.
    public List<Platillo> Platillos { get; set; } = new();
}

// Platillo representa una opción vendible del menú e incluye inventario y foto persistente.
public class Platillo {
    [Key] public int IdPlatillo { get; set; }
    // Nombre de menú, obligatorio y limitado para que pueda almacenarse y presentarse de forma consistente.
    [Required(ErrorMessage = "El nombre es obligatorio"), StringLength(150)] public string Nombre { get; set; } = "";
    // Texto opcional de presentación, limitado a 300 caracteres.
    [StringLength(300)] public string? Descripcion { get; set; }
    // Precio vigente; los detalles de una orden copian este valor para preservar el precio de la venta.
    [Range(0.01, 10000, ErrorMessage = "Precio inválido"), Column(TypeName = "decimal(10,2)")] public decimal Precio { get; set; }
    // Estado de venta (por ejemplo Disponible o Agotado); las consultas públicas filtran por Disponible.
    [Required, StringLength(30)] public string Estado { get; set; } = "Disponible";
    // Unidades que aún pueden pedirse; las órdenes reducen existencias y al llegar a cero agotan el platillo.
    [Range(0, 100000)] public int Existencias { get; set; } = 0;
    // Bytes originales de la imagen, guardados en SQL para no depender del disco temporal del contenedor.
    // La carga valida tamaño y firma de JPEG, PNG o WebP antes de asignar estos bytes.
    [Column(TypeName = "varbinary(max)")] public byte[]? Imagen { get; set; }
    // Tipo MIME detectado a partir de la firma del archivo; se usa al responder la petición de la imagen.
    [StringLength(100)] public string? ImagenContentType { get; set; }
    // Clave foránea obligatoria que relaciona el platillo con una categoría.
    public int IdCategoria { get; set; }
    // Navegación opcional de EF Core para mostrar el nombre de categoría sin consulta separada.
    public Categoria? Categoria { get; set; }
}

// Orden agrupa los productos solicitados para una mesa y su ciclo de preparación/facturación.
public class Orden {
    [Key] public int IdOrden { get; set; }
    // Momento de creación; los reportes y la cola de cocina usan esta fecha para ordenar o filtrar.
    public DateTime Fecha { get; set; } = DateTime.Now;
    // Flujo principal: Pendiente, En preparación, Lista y Facturada.
    [Required, StringLength(30)] public string Estado { get; set; } = "Pendiente";
    // Mesa obligatoria; queda ocupada hasta que todas sus órdenes se hayan facturado.
    public int IdMesa { get; set; }
    public Mesa? Mesa { get; set; }
    // Personal que creó la orden; es nulo en pedidos enviados directamente por clientes anónimos.
    public int? IdUsuario { get; set; }
    public Usuario? Usuario { get; set; }
    // Hash de un token aleatorio de cliente anónimo, válido solo para continuar mientras la orden esté abierta.
    [StringLength(64)] public string? TokenPedidoPublico { get; set; }
    // Líneas de productos de la orden, incluyendo cantidad y precio congelado al momento de pedir.
    public List<DetalleOrden> Detalles { get; set; } = new();
}

// DetalleOrden conserva la instantánea comercial de cada platillo dentro de una orden.
public class DetalleOrden {
    [Key] public int IdDetalle { get; set; }
    // Unidades de este platillo solicitadas en la orden.
    [Range(1, 100)] public int Cantidad { get; set; }
    // Precio por unidad copiado desde Platillo al agregarlo, no recalculado al consultar después.
    [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "El precio debe ser mayor que $0.00")]
    [Column(TypeName = "decimal(10,2)")] public decimal PrecioUnitario { get; set; }
    // Importe de línea calculado como Cantidad por PrecioUnitario y usado para subtotal/factura.
    [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "El subtotal debe ser mayor que $0.00")]
    [Column(TypeName = "decimal(10,2)")] public decimal Subtotal { get; set; }
    public int IdOrden { get; set; }
    public Orden? Orden { get; set; }
    public int IdPlatillo { get; set; }
    // Navegación para mostrar nombre/foto actual; el precio histórico es PrecioUnitario.
    public Platillo? Platillo { get; set; }
}

// Factura registra el cierre financiero de una orden; sus importes se calculan al emitirla.
public class Factura {
    [Key] public int IdFactura { get; set; }
    // Fecha de emisión, distinta de la fecha de creación de la orden.
    public DateTime Fecha { get; set; } = DateTime.Now;
    // Suma de los subtotales de las líneas de la orden, antes de impuestos.
    [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "El subtotal debe ser mayor que $0.00")]
    [Column(TypeName = "decimal(10,2)")] public decimal Subtotal { get; set; }
    // Impuesto calculado por el controlador con la tasa configurada para el restaurante.
    [Column(TypeName = "decimal(10,2)")] public decimal IVA { get; set; }
    // Total a cobrar: Subtotal más IVA.
    [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "El total debe ser mayor que $0.00")]
    [Column(TypeName = "decimal(10,2)")] public decimal Total { get; set; }
    public int IdOrden { get; set; }
    public Orden? Orden { get; set; }
    // Pagos registrados contra esta factura; el flujo actual agrega un pago por el total.
    public List<Pago> Pagos { get; set; } = new();
}

// Pago representa el importe recibido y el medio usado para cubrir una factura.
public class Pago {
    [Key] public int IdPago { get; set; }
    // Método elegido en caja, por ejemplo Efectivo, Tarjeta o Transferencia.
    [Required, StringLength(50)] public string MetodoPago { get; set; } = "Efectivo";
    // Importe asociado al pago; al emitir factura se inicializa con el total facturado.
    [Column(TypeName = "decimal(10,2)")] public decimal Monto { get; set; }
    // Momento en que se registra el pago.
    public DateTime FechaPago { get; set; } = DateTime.Now;
    public int IdFactura { get; set; }
    public Factura? Factura { get; set; }
}
