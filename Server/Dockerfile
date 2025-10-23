# --- Estágio 1: Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# CORREÇÃO: Copie o arquivo de solução (.sln) e todos os .csproj para restaurar dependências de forma robusta.
# Esta abordagem é menos propensa a erros de caminho e mais fácil de manter.

# 1. Copie o arquivo da solução
# (Ajuste o nome 'PitfallServer.sln' se o seu for diferente)
COPY ["Solution.sln", "."]

# 2. Copie os arquivos .csproj de cada projeto para suas respectivas pastas
COPY ["Server/Server.csproj", "Server/"]
COPY ["Controllers/Controllers.csproj", "Controllers/"]
COPY ["Data/Data.csproj", "Data/"]
COPY ["DTOs/DTOs.csproj", "DTOs/"]
COPY ["Services/Services.csproj", "Services/"]

# 3. Restaure todos os pacotes NuGet baseando-se nos projetos copiados
RUN dotnet restore "Solution.sln"

# 4. Copie todo o resto do código fonte
COPY . .

# 5. Mova para a pasta do projeto principal e execute o build
WORKDIR "/src/Server"
RUN dotnet build "Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# --- Estágio 2: Publish ---
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# --- Estágio 3: Final ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Exponha as portas corretas que o Kestrel vai usar
EXPOSE 8080
EXPOSE 443
ENTRYPOINT ["dotnet", "Server.dll"]