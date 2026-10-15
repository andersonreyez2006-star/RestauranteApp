using Microsoft.EntityFrameworkCore;
using RestauranteApp.Models;
namespace RestauranteApp.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Mesa> Mesas => Set<Mesa>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Platillo> Platillos => Set<Platillo>();
    public DbSet<Orden> Ordenes => Set<Orden>();
    public DbSet<DetalleOrden> Detalles => Set<DetalleOrden>();
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<Pago> Pagos => Set<Pago>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Usuario>().HasIndex(u => u.Correo).IsUnique();
        b.Entity<Mesa>().HasIndex(m => m.NumeroMesa).IsUnique();
        b.Entity<Factura>().HasIndex(f => f.IdOrden).IsUnique().HasDatabaseName("UX_Facturas_IdOrden");
        b.Entity<Orden>().HasIndex(o => o.TokenPedidoPublico).IsUnique()
            .HasDatabaseName("UX_Ordenes_TokenPedidoPublico").HasFilter("[TokenPedidoPublico] IS NOT NULL");
        // Evita rutas de borrado en cascada múltiples en SQL Server
        b.Entity<Orden>().HasOne(o => o.Mesa).WithMany().HasForeignKey(o => o.IdMesa).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Orden>().HasOne(o => o.Usuario).WithMany().HasForeignKey(o => o.IdUsuario).OnDelete(DeleteBehavior.Restrict);
        b.Entity<DetalleOrden>().HasOne(d => d.Platillo).WithMany().HasForeignKey(d => d.IdPlatillo).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Platillo>().HasOne(p => p.Categoria).WithMany(c => c.Platillos).HasForeignKey(p => p.IdCategoria).OnDelete(DeleteBehavior.Restrict);

        b.Entity<Categoria>().HasData(
            new Categoria { IdCategoria = 1, Nombre = "Entradas" },
            new Categoria { IdCategoria = 2, Nombre = "Platos fuertes" },
            new Categoria { IdCategoria = 3, Nombre = "Postres" },
            new Categoria { IdCategoria = 4, Nombre = "Bebidas" });
        b.Entity<Platillo>().HasData(
            new Platillo { IdPlatillo = 1, Nombre = "Pupusas revueltas", Descripcion = "Tres pupusas con curtido y salsa", Precio = 3.50m, IdCategoria = 1, Existencias = 50 },
            new Platillo { IdPlatillo = 2, Nombre = "Yuca frita con chicharrón", Descripcion = "Crujiente, con curtido", Precio = 5.00m, IdCategoria = 1, Existencias = 25 },
            new Platillo { IdPlatillo = 3, Nombre = "Pollo encebollado", Descripcion = "Pollo a la plancha con arroz y ensalada", Precio = 7.25m, IdCategoria = 2, Existencias = 20 },
            new Platillo { IdPlatillo = 4, Nombre = "Filete de res a la parrilla", Descripcion = "Con papas y vegetales", Precio = 11.90m, IdCategoria = 2, Existencias = 15 },
            new Platillo { IdPlatillo = 5, Nombre = "Quesadilla salvadoreña", Descripcion = "Postre dulce de queso, ideal con café", Precio = 2.75m, IdCategoria = 3, Existencias = 30 },
            new Platillo { IdPlatillo = 6, Nombre = "Horchata", Descripcion = "Bebida fría de morro", Precio = 1.75m, IdCategoria = 4, Existencias = 40 });
        b.Entity<Mesa>().HasData(
            new Mesa { IdMesa = 1, NumeroMesa = 1, Capacidad = 2 },
            new Mesa { IdMesa = 2, NumeroMesa = 2, Capacidad = 4 },
            new Mesa { IdMesa = 3, NumeroMesa = 3, Capacidad = 6 });
    }
}
