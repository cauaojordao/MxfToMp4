FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Worker/Worker.csproj", "Worker/"]
RUN dotnet restore "Worker/Worker.csproj"

COPY . .
WORKDIR "/src/Worker"
RUN dotnet build "./Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final

# Instalar ffmpeg no estágio final
RUN apt-get update && apt-get install -y ffmpeg

# Verificar a instalação
RUN which ffmpeg && which ffprobe && ffmpeg -version

# Criar diretório temporário
RUN mkdir -p /app/temp && chmod -R 777 /app/temp

WORKDIR /app

# Copiar os arquivos publicados
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Worker.dll"]