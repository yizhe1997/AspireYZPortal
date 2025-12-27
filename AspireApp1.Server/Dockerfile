# Multi-stage Dockerfile for AspireApp1.Server (targets .NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy csproj and restore
COPY ["AspireApp1.Server/AspireApp1.Server.csproj", "AspireApp1.Server/"]
RUN dotnet restore "AspireApp1.Server/AspireApp1.Server.csproj"

# copy everything and publish
COPY . .
WORKDIR /src/AspireApp1.Server
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
ENTRYPOINT ["dotnet", "AspireApp1.Server.dll"]
