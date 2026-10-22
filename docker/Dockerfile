FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/RestauranteApp/RestauranteApp.csproj", "./"]
RUN dotnet restore "RestauranteApp.csproj"
COPY src/RestauranteApp/ .
RUN dotnet publish "RestauranteApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RestauranteApp.dll"]
