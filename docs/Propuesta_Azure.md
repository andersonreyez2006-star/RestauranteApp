# Propuesta de despliegue en Microsoft Azure

## Arquitectura implementada

```text
Navegador
   │ HTTPS
   ▼
Azure App Service (contenedor ASP.NET Core)
   │ conexión configurada fuera del repositorio
   ▼
Azure SQL Database

GitHub Actions ── construye imagen ──► Azure Container Registry
        └──── despliega imagen ─────► Azure App Service
```

El repositorio ya tiene un workflow que construye el contenedor, lo publica en ACR y lo despliega en el App Service `DSP`. El contenedor usa .NET 8 y escucha en el puerto 8080. La aplicación no usa servicios de IA de Azure.

## Servicios y beneficios

- **Azure App Service for Containers:** aloja la aplicación web administrada y recibe despliegues del workflow.
- **Azure Container Registry (ACR):** conserva las imágenes construidas por GitHub Actions.
- **Azure SQL Database:** almacena el catálogo, usuarios, órdenes y facturación de producción.
- **GitHub Actions:** automatiza compilación de imagen y despliegue al integrar cambios en `master`.

Separar la base de datos del contenedor permite que los datos sobrevivan a reinicios y nuevas versiones.

## Seguridad de configuración

La cadena `ConnectionStrings__Default` debe configurarse como variable secreta del App Service. ACR debe permitir al App Service descargar imágenes con el mecanismo de identidad configurado para el entorno. No publicar SQL Server Developer ni el puerto 1433 a Internet. Mantener credenciales fuera del código, historial Git y logs.

## Estimación de costos

El repositorio no fija región, nivel/SKU, redundancia, volumen de datos ni horas de uso; por ello una cifra mensual exacta sería engañosa. Antes de entregar presupuesto, seleccionar región y niveles en la [Calculadora de precios de Azure](https://azure.microsoft.com/pricing/calculator/) y registrar la fecha y supuestos. Considerar como mínimo App Service, ACR, Azure SQL, almacenamiento/respaldos, transferencia de datos e impuestos. Para una demo estudiantil, evaluar niveles de desarrollo/prueba y detener recursos no usados, verificando sus límites y disponibilidad en la región.

## Referencias de configuración

- Workflow: `.github/workflows/master_dsp.yml`
- Contenedor: `docker/Dockerfile`
- Conexión local: `docker/compose.yaml`
- Instrucciones: [Manual de instalación](Manual_Instalacion.md)
