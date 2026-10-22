# Base de datos

La aplicación utiliza SQL Server y Entity Framework Core. El contexto, entidades, relaciones e información semilla están en `src/RestauranteApp/Data/AppDbContext.cs` y `src/RestauranteApp/Models/Entities.cs`.

## Entidades principales

- `Usuario`: cuentas y roles.
- `Mesa`: número, capacidad y estado.
- `Categoria` y `Platillo`: catálogo y existencias.
- `Orden` y `DetalleOrden`: pedidos y partidas.
- `Factura` y `Pago`: facturación y pagos.

## Creación y actualización

Al arrancar, la aplicación aplica migraciones EF Core disponibles; si no hay migraciones, utiliza `EnsureCreated`, junto con actualizaciones SQL aditivas para instalaciones previas. El contexto incluye datos semilla de desarrollo. En producción, se requiere una cadena de conexión privada suministrada por el entorno.

El contenedor SQL Server Developer de `docker/compose.yaml` es únicamente para desarrollo local. No usarlo como base productiva ni exponer el puerto de base de datos a Internet.

## Protección de datos

El endpoint público del FAQ no consulta `Usuario`, `Orden`, `Factura` ni `Pago`; consulta únicamente platillos listados públicamente (disponibles, con existencia positiva y precio mayor a cero) y selecciona nombre, categoría, descripción y precio. No entrega cantidades exactas del inventario.
