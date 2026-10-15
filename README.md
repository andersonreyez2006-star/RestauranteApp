# Sistema Web de Gestión Gastronómica (DSP – Fase II)
ASP.NET Core MVC (.NET 8) · Entity Framework Core · SQL Server · Docker · Asistente de IA (Azure OpenAI)

## Ejecutar con Docker
1. `cp .env.example .env` y cambia `SA_PASSWORD` (opcional: credenciales de Azure OpenAI).
2. `docker compose --env-file .env -f docker/docker-compose.yml up --build`
3. Abrir http://localhost:8080

## Ejecutar sin Docker
1. Visual Studio / VS Code con .NET 8 SDK y SQL Server LocalDB.
2. `cd src/RestauranteApp && dotnet run`

## Estructura
`/src` código · `/docs` manuales, pruebas y Azure · `/database` diagrama y scripts · `/docker` Dockerfile y compose

## Usuarios demo
administrador@restaurante.com / Admin123! · mesero@restaurante.com / Mesero123! · cocina@restaurante.com / Cocina123! · cajero@restaurante.com / Cajero123!

## Documentación (`/docs`)
Manual_Instalacion · Manual_Usuario · Manual_Tecnico · Propuesta_Azure · Plan_de_Pruebas · Guion_Video · Guia_Git

