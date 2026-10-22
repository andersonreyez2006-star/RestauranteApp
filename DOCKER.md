# Ejecutar RestauranteApp con Docker

## Requisitos

- Docker Desktop instalado y en ejecución.
- Docker Compose disponible con `docker compose`.

## Iniciar en desarrollo local

1. En la carpeta que contiene `Dockerfile`, copia `.env.example` como `.env`.
2. Cambia `SA_PASSWORD` en `.env` por una contraseña segura. Para habilitar Gemini, define `GOOGLE_AI_API_KEY` en `.env`. Si no configuras la clave o Gemini falla, el asistente usa el FAQ local como respaldo. No publiques el archivo `.env`.
3. Abre una terminal en la carpeta del proyecto y ejecuta:

   ```powershell
   docker compose up --build
   ```

4. Abre <http://localhost:8080>.

La primera vez, SQL Server puede tardar en iniciar. La aplicación reintenta errores transitorios de conexión y crea el esquema y los usuarios de demostración cuando la base `Restaurante` está vacía. Compose usa `ASPNETCORE_ENVIRONMENT=Development` para este uso local; por eso no requiere `InitialAdmin__Email` ni `InitialAdmin__Password`. Las cuentas de demostración solo se crean en desarrollo.

Para detener los contenedores, usa `Ctrl+C` y luego:

```powershell
docker compose down
```

Los datos de SQL Server se conservan en el volumen `restaurante_sql_data`. Para reiniciar la base vacía se puede ejecutar `docker compose down -v`; **ese comando elimina permanentemente los datos guardados en ese volumen**.

## Construir solo la imagen web

```powershell
docker build -t restaurante-app:local .
```

La imagen sirve HTTP en el puerto `8080`. La aplicación recibe la cadena de conexión por `ConnectionStrings__Default`; no se debe poner la contraseña de SQL Server en el código ni en `appsettings.json`.

## Preparar Azure

El `compose.yaml` anterior es para pruebas locales: usa un contenedor SQL Server Developer. Para producción en Azure, publica la imagen de `web` en Azure Container Registry y configura Azure App Service for Containers o Azure Container Apps para usarla. Conecta la aplicación a Azure SQL Database mediante la configuración segura del servicio, preferentemente con identidad administrada. No despliegues el contenedor SQL Server Developer como base de datos de producción ni publiques el puerto `1433` a Internet.

El chatbot usa Gemini de Google Cloud Agent Platform Express Mode cuando `GOOGLE_AI_API_KEY` está configurada y envía la pregunta junto con el catálogo actual de platillos disponibles. Si la clave falta, la API falla o se agota el tiempo de espera, vuelve al FAQ local para que el chat siga respondiendo. En Azure, configura `GOOGLE_AI_API_KEY` como variable secreta del App Service; la aplicación no usa servicios de IA de Azure. Para horarios, pagos, alérgenos u otros datos no registrados, indica consultar al personal.

Las fotos de los platillos se almacenan en Azure SQL junto con sus registros para que sobrevivan a los reinicios y despliegues del contenedor. Se aceptan JPEG, PNG y WebP de hasta 5 MB. El esquema existente agrega las columnas de imagen de forma aditiva al iniciar la aplicación.

### Despliegue automatizado en App Service

El workflow `.github/workflows/master_dsp.yml` construye la imagen desde este `Dockerfile`, la publica en `acrrestaurante2026.azurecr.io` y la despliega en el App Service `DSP` al actualizar `master`, o manualmente desde GitHub Actions.

Antes de ejecutarlo:

1. Crea los secretos de acceso a Azure que utiliza `.github/workflows/master_dsp.yml` en **Settings → Secrets and variables → Actions** del repositorio.
2. Configura el acceso del App Service `DSP` al ACR `acrrestaurante2026`.
3. En **Configuración → Variables de entorno** del App Service, agrega `ConnectionStrings__Default` con la cadena de Azure SQL Database. Guárdala como configuración marcada como secreta; no la escribas en el repositorio ni en GitHub Actions.
   Para habilitar Gemini, agrega también `GOOGLE_AI_API_KEY` como variable secreta. Esta clave es para Google y no requiere un servicio de IA de Azure.
   Si la base está vacía, configura también `InitialAdmin__Email` y `InitialAdmin__Password` para crear la cuenta inicial de administrador. La contraseña se almacena hasheada. Elimina esas variables después de crear la cuenta; cambia o elimina cualquier cuenta de demostración que ya exista en una base de producción.
4. En el servidor SQL, permite la conexión de red del App Service mediante sus direcciones IP de salida en **Redes → Firewalls y redes virtuales**. No habilites el acceso de todos los servicios de Azure si no es necesario.
5. Integra estos cambios en `master` o `main` y revisa la ejecución del workflow en la pestaña **Actions** de GitHub.

La imagen usa una etiqueta única por commit y también actualiza `latest`. El App Service usa su identidad administrada para descargar la imagen privada; no se guardan credenciales del ACR en el repositorio.
