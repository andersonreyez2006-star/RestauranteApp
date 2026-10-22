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

`POST /Asistente/Preguntar` es anónimo y limitado a 10 solicitudes por minuto por IP; rechaza texto vacío y preguntas de más de 500 caracteres. Consulta solo platillos con estado disponible, existencias mayores a cero y precio positivo, y proyecta únicamente nombre, categoría, descripción y precio. No accede a usuarios, órdenes, facturas, pagos ni reportes, ni revela conteos de inventario.

`POST /Asistente/PreguntarAdministrador` exige el rol `Administrador` y aplica el mismo límite de longitud y frecuencia. El chat administrativo responde en modo de solo lectura con ventas facturadas del día, órdenes abiertas, existencias, alertas de cinco unidades o menos y datos del platillo consultado. Proyecta solo los campos operativos necesarios; no lee credenciales ni devuelve información personal de clientes. La autorización está en el endpoint del servidor y no depende de que el botón esté oculto en la interfaz.

El asistente utiliza coincidencia de palabras normalizadas y respuestas deterministas. Sus recomendaciones se seleccionan de los platillos públicos actuales; no se debe describir como modelo de IA generativa. Para presentarlo como cumplimiento de un requisito de IA, el equipo debe validar con el docente si un FAQ/reglas satisface la propuesta aprobada en Investigación Aplicada II.

Las preguntas públicas sobre clima se resuelven con `ClimaService`, que consulta la geocodificación y el pronóstico actual de Open-Meteo. No requiere API key ni configuración en Azure. Solo se envía a ese proveedor el nombre de ciudad escrito en la pregunta; nunca se envían datos del menú, clientes, pedidos o cuentas. Se establece un timeout de 10 segundos y los errores de red, timeout o respuesta JSON se notifican al usuario sin interrumpir otras consultas del FAQ.

## API web del chatbot

- Método y ruta: `POST /Asistente/Preguntar`
- Entrada JSON: `{"texto":"¿Qué postres tienen?"}`
- Respuesta JSON: `{"respuesta":"..."}`.
- Límite de texto: 500 caracteres.
- Ruta administrativa: `POST /Asistente/PreguntarAdministrador` (requiere rol `Administrador`).
- Controlador: `src/RestauranteApp/Controllers/AsistenteController.cs`.

Este endpoint es un servicio web propio y no usa IA generativa; cuando la pregunta es sobre clima, el servidor consulta Open-Meteo.

La integración meteorológica consume los endpoints públicos de geocodificación y pronóstico de Open-Meteo desde el servidor; no expone una API key al navegador.

## Configuración y publicación

La cadena `ConnectionStrings__Default` se inyecta desde el entorno. No se deben versionar secretos. El `Dockerfile` usa `docker/Dockerfile`; el contexto de construcción es la raíz del repositorio. El workflow GitHub Actions está en `.github/workflows/master_dsp.yml`.

## Extensión

Para añadir respuestas, amplía `src/RestauranteApp/Services/AsistenteService.cs` y agrega casos al plan de pruebas. Mantén los datos públicos en `Preguntar`; cualquier dato operacional debe quedarse en la ruta administrativa protegida y no debe incluir información personal.
