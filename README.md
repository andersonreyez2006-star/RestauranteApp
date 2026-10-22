# RestauranteApp — Proyecto DSP Fase II

Aplicación web de gestión de restaurante construida con ASP.NET Core MVC (.NET 8), Entity Framework Core y SQL Server. Incluye gestión de menú, categorías, mesas, pedidos, cocina, facturación, reportes y un asistente FAQ que responde con información pública del menú.

## Estructura del repositorio

```text
src/RestauranteApp/   Aplicación ASP.NET Core MVC
tests/                Pruebas automatizadas de lógica y privacidad del FAQ
docs/                 Manuales, propuesta Azure, pruebas y guion de video
database/             Información del modelo y ciclo de vida de la base de datos
docker/               Dockerfile y Docker Compose
.env.example          Plantilla de configuración local (sin secretos)
```

## Ejecutar con Docker

Requiere Docker Desktop y Docker Compose. En PowerShell, desde la raíz:

```powershell
Copy-Item .env.example .env
# Edita .env y reemplaza SA_PASSWORD por una clave local segura.
docker compose --env-file .env -f docker/compose.yaml up --build
```

Abre <http://localhost:8080>. La base local conserva datos en un volumen de Docker. Para detenerla, usa `Ctrl+C` y ejecuta `docker compose --env-file .env -f docker/compose.yaml down`.

## Ejecutar con .NET

Requiere .NET 8 SDK y un SQL Server disponible. Configura `ConnectionStrings__Default` para ese servidor y ejecuta:

```powershell
dotnet run --project src/RestauranteApp/RestauranteApp.csproj
```

## Asistente FAQ y privacidad

El asistente **no usa una IA externa**. Los visitantes consultan únicamente información pública de platillos disponibles; el administrador autenticado tiene un chat separado de solo lectura para consultar ventas del día, órdenes abiertas e inventario. El endpoint administrativo está protegido por rol y no muestra datos personales de clientes ni credenciales.

## Documentación

- [Manual de instalación](docs/Manual_Instalacion.md)
- [Manual de usuario](docs/Manual_Usuario.md)
- [Manual técnico](docs/Manual_Tecnico.md)
- [Propuesta de despliegue Azure](docs/Propuesta_Azure.md)
- [Plan de pruebas](docs/Plan_de_Pruebas.md)
- [Guion del video demostrativo](docs/Guion_Video.md)
- [Modelo y datos de base de datos](database/README.md)
