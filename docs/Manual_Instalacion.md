# Manual de instalación

## Requisitos

- .NET 8 SDK para ejecutar desde el equipo.
- Docker Desktop con Docker Compose para ejecutar contenedores.
- SQL Server local o remoto si se ejecuta sin Docker.

## Obtener y configurar el proyecto

Clona el repositorio y entra en la carpeta raíz:

```powershell
git clone https://github.com/andersonreyez2006-star/RestauranteApp.git
Set-Location RestauranteApp
```

### Ejecución con Docker

1. Copia la plantilla y edita la contraseña local de SQL Server:

   ```powershell
   Copy-Item .env.example .env
   ```

2. Construye e inicia los contenedores desde la raíz:

   ```powershell
   docker compose --env-file .env -f docker/compose.yaml up --build
   ```

3. Visita <http://localhost:8080>.
4. Detén con `Ctrl+C`; para apagar los servicios ejecuta:

   ```powershell
   docker compose --env-file .env -f docker/compose.yaml down
   ```

SQL Server guarda la base en el volumen `restaurante_sql_data`. `docker compose ... down -v` borra ese volumen y sus datos; úsalo solo cuando quieras eliminar la base local.

### Ejecución con .NET

1. Configura la variable `ConnectionStrings__Default` para apuntar a un SQL Server disponible. No guardes la contraseña en Git.
2. Desde la raíz, restaura dependencias y ejecuta:

   ```powershell
   dotnet restore src/RestauranteApp/RestauranteApp.csproj
   dotnet run --project src/RestauranteApp/RestauranteApp.csproj
   ```

3. Abre la URL local que muestre la consola.

### Ejecutar las pruebas automatizadas

Desde la raíz, restaura y ejecuta las pruebas de privacidad/respuestas del FAQ:

```powershell
dotnet test tests/RestauranteApp.Tests/RestauranteApp.Tests.csproj
```

Entity Framework Core crea o actualiza el esquema al iniciar. Las cuentas de demostración se crean únicamente en el entorno de desarrollo.

## Variables de entorno

`.env.example` contiene únicamente la plantilla para Docker. Cambia `SA_PASSWORD` antes de iniciar. `ConnectionStrings__Default` se configura en el entorno de ejecución o en el App Service; nunca incluyas una cadena con credenciales en el repositorio.

## Despliegue

La integración continua está descrita en [Propuesta_Azure.md](Propuesta_Azure.md). Los cambios integrados en `master` disparan GitHub Actions para construir la imagen del Dockerfile ubicado en `docker/` y desplegarla al App Service configurado.
