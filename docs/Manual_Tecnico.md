# Manual técnico

## Tecnologías y arquitectura

- ASP.NET Core MVC en .NET 8 con vistas Razor.
- Entity Framework Core 8 y SQL Server.
- Autenticación mediante cookies y autorización basada en roles.
- Docker y Docker Compose para ejecución local; GitHub Actions construye y despliega la imagen.

La aplicación sigue una estructura MVC por capas: `Controllers` reciben peticiones y aplican autorización/validación; `Services` contiene la lógica del asistente FAQ; `Data` configura `AppDbContext`; `Models` declara entidades y modelos de vista; `Views` presenta las páginas Razor.

## Datos

`AppDbContext` declara usuarios, mesas, categorías, platillos, órdenes, detalles, facturas y pagos. Las relaciones, índices únicos y datos semilla están configurados en `src/RestauranteApp/Data/AppDbContext.cs`. El arranque aplica migraciones EF Core disponibles o usa `EnsureCreated` cuando no hay migraciones, además de ejecutar actualizaciones aditivas para instalaciones previas.

## Seguridad y privacidad del asistente

`POST /Asistente/Preguntar` es anónimo y limitado a 10 solicitudes por minuto por IP; rechaza texto vacío y preguntas de más de 500 caracteres. `AsistenteService` consulta solo platillos con estado disponible, existencias mayores a cero y precio positivo, y proyecta únicamente nombre, categoría, descripción y precio. La consulta no accede a entidades de usuarios, órdenes, facturas, pagos ni reportes. No revela los conteos de inventario.

El asistente utiliza coincidencia de palabras normalizadas y respuestas deterministas. Sus recomendaciones se seleccionan de los platillos públicos actuales; no se debe describir como modelo de IA generativa. Para presentarlo como cumplimiento de un requisito de IA, el equipo debe validar con el docente si un FAQ/reglas satisface la propuesta aprobada en Investigación Aplicada II.

## API web del chatbot

- Método y ruta: `POST /Asistente/Preguntar`
- Entrada JSON: `{"texto":"¿Qué postres tienen?"}`
- Respuesta JSON: `{"respuesta":"..."}`.
- Límite de texto: 500 caracteres.
- Controlador: `src/RestauranteApp/Controllers/AsistenteController.cs`.

Este endpoint es un servicio web propio; no consume servicios de IA ni API externas.

## Configuración y publicación

La cadena `ConnectionStrings__Default` se inyecta desde el entorno. No se deben versionar secretos. El `Dockerfile` usa `docker/Dockerfile`; el contexto de construcción es la raíz del repositorio. El workflow GitHub Actions está en `.github/workflows/master_dsp.yml`.

## Extensión

Para añadir respuestas al FAQ, amplía las reglas de `src/RestauranteApp/Services/AsistenteService.cs`, usa únicamente columnas públicas del menú y agrega casos al plan de pruebas. No uses datos personales o transaccionales en un endpoint anónimo.
