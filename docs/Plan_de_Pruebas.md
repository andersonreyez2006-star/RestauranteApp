# Plan de pruebas funcionales e integración

No se registran resultados que no hayan sido ejecutados. Completa la columna “Resultado” con fecha, entorno y evidencia (captura o enlace) al realizar cada prueba.

| ID | Caso | Pasos resumidos | Resultado esperado | Resultado |
|---|---|---|---|---|
| FAQ-01 | Saludo y ayuda | Abrir chat y escribir “hola” | Saludo amable con capacidades del FAQ | Pendiente |
| FAQ-02 | Menú público | Preguntar “¿Qué platillos tienen?” | Solo enumera platillos disponibles del catálogo público | Automatizada |
| FAQ-03 | Categorías | Preguntar “¿Qué postres hay?” | Lista solo opciones disponibles de esa categoría | Pendiente |
| FAQ-04 | Descripción | Preguntar por descripción de un platillo existente y disponible | Devuelve descripción y precio registrados | Automatizada |
| FAQ-05 | Precio | Preguntar precio por nombre y “¿qué es lo más barato?” | Usa precios vigentes del catálogo | Pendiente |
| FAQ-06 | Privacidad de inventario | Preguntar cantidades exactas en inventario | No revela unidades; solo confirma disponibilidad pública | Automatizada |
| FAQ-07 | Producto agotado | Agotar un platillo y consultarlo como cliente | No aparece entre opciones disponibles y no filtra sus existencias | Automatizada |
| FAQ-08 | Recomendación | Preguntar “¿Qué me recomiendas?” | Recomienda hasta tres platillos disponibles del menú | Pendiente |
| FAQ-09 | Pedido público | Preguntar cómo pedir en mesa | Indica pasos de selección de mesa y envío a cocina | Pendiente |
| API-01 | Validación | Enviar vacío o más de 500 caracteres a `POST /Asistente/Preguntar` | Respuesta 400 con mensaje de validación | Pendiente |
| API-02 | Límite | Enviar más de 10 preguntas en un minuto desde una IP | Las solicitudes excedentes reciben 429 | Pendiente |
| CRUD-01 | Catálogo | Crear/editar/borrar categoría y platillo con rol autorizado | Cambios válidos persistidos; restricciones informadas | Pendiente |
| AUTH-01 | Roles | Intentar abrir páginas administrativas sin sesión o sin rol | Acceso denegado/retorno a login; no muestra datos privados | Pendiente |
| DOCKER-01 | Contenedores | Ejecutar Compose desde la raíz siguiendo el manual | Aplicación y SQL Server inician; web responde en puerto 8080 | Pendiente |
| AZ-01 | Despliegue | Revisar workflow de GitHub Actions | Build y job de despliegue exitosos; sitio responde por HTTPS | Pendiente |

## Evidencias por recopilar

Guardar capturas anonimizadas en `docs/evidencias/` o enlazar registros de GitHub Actions. No incluir contraseñas, tokens, claves, información de clientes ni datos reales de pago. Las pruebas de producción no deben crear pedidos/facturas ficticios sin autorización.

## Estado de verificación de código

El FAQ consulta únicamente platillos públicos y el controlador limita longitud y frecuencia. La compilación local debe repetirse después de cambios. Los resultados de casos funcionales, contenedor y Azure deben completarse ejecutando cada caso; no se infieren a partir de la compilación.

Las pruebas automatizadas de consulta pública e inventario están en `tests/RestauranteApp.Tests/AsistenteServiceTests.cs`; ejecuta el comando de [instalación](Manual_Instalacion.md) para confirmarlas en el entorno actual.
