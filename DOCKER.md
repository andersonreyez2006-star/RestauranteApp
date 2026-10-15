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

La primera vez, SQL Server puede tardar en iniciar. La aplicación reintenta la conexión durante aproximadamente un minuto y crea el esquema y los datos iniciales cuando la base `Restaurante` está vacía.

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
