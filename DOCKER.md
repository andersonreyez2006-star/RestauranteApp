# Ejecutar RestauranteApp con Docker

## Requisitos

- Docker Desktop instalado y en ejecución.
- Docker Compose disponible con `docker compose`.

## Iniciar en desarrollo local

1. En la carpeta que contiene `Dockerfile`, copia `.env.example` como `.env`.
2. Cambia `SA_PASSWORD` en `.env` por una contraseña segura. No compartas ni publiques ese archivo.
3. Abre una terminal en la carpeta del proyecto y ejecuta:

   ```powershell
   docker compose up --build
   ```

4. Abre <http://localhost:8080>.

La primera vez, SQL Server puede tardar en iniciar. La aplicación reintenta errores transitorios de conexión y crea el esquema y los usuarios de demostración cuando la base `Restaurante` está vacía. Las cuentas de demostración solo se crean en desarrollo.

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

El chat público responde preguntas frecuentes sobre el menú, precios y pedidos usando los datos actuales de platillos disponibles. No llama a Foundry ni a otro proveedor externo y no requiere claves o variables de IA en App Service. Sus respuestas están basadas en reglas explícitas; para horarios, pagos, alérgenos u otros datos no registrados, recomienda consultar al personal.

Las fotos de los platillos se almacenan en Azure SQL junto con sus registros para que sobrevivan a los reinicios y despliegues del contenedor. Se aceptan JPEG, PNG y WebP de hasta 5 MB. El esquema existente agrega las columnas de imagen de forma aditiva al iniciar la aplicación.

### Despliegue automatizado en App Service

El workflow `.github/workflows/azure-deploy.yml` construye la imagen desde este `Dockerfile`, la publica en `acrrestaurante2026.azurecr.io` y configura el App Service `DSPproy` para usarla en el puerto `8080`. Se ejecuta al actualizar `master` o `main`, o manualmente desde GitHub Actions.

Antes de ejecutarlo:

1. Configura autenticación federada de GitHub Actions (OIDC) para la aplicación de Microsoft Entra ID y crea estos secretos en **Settings → Secrets and variables → Actions** del repositorio: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` y `AZURE_SUBSCRIPTION_ID`. La identidad necesita permisos para publicar imágenes en el ACR y actualizar el App Service.
2. En el App Service `DSPproy`, habilita la identidad administrada asignada por el sistema. En el ACR `acrrestaurante2026`, asigna a esa identidad el rol **AcrPull**.
3. En **Configuración → Variables de entorno** del App Service, agrega `ConnectionStrings__Default` con la cadena de Azure SQL Database. Guárdala como configuración marcada como secreta; no la escribas en el repositorio ni en GitHub Actions.
   Si la base está vacía, configura también `InitialAdmin__Email` y `InitialAdmin__Password` para crear la cuenta inicial de administrador. La contraseña se almacena hasheada. Elimina esas variables después de crear la cuenta; cambia o elimina cualquier cuenta de demostración que ya exista en una base de producción.
4. En el servidor SQL, permite la conexión de red del App Service mediante sus direcciones IP de salida en **Redes → Firewalls y redes virtuales**. No habilites el acceso de todos los servicios de Azure si no es necesario.
5. Integra estos cambios en `master` o `main` y revisa la ejecución del workflow en la pestaña **Actions** de GitHub.

La imagen usa una etiqueta única por commit y también actualiza `latest`. El App Service usa su identidad administrada para descargar la imagen privada; no se guardan credenciales del ACR en el repositorio.
