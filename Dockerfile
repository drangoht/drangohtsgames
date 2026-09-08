# syntax=docker/dockerfile:1

# --- Étape de compilation ----------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /source

# Les fichiers projet d'abord : tant qu'aucune dépendance ne change, la couche de
# restauration est réutilisée telle quelle d'un build à l'autre.
COPY Directory.Build.props Directory.Build.targets global.json ./
COPY src/DrangohtGames.slnx src/
COPY src/DrangohtGames.Web/DrangohtGames.Web.csproj src/DrangohtGames.Web/
COPY tests/DrangohtGames.Tests/DrangohtGames.Tests.csproj tests/DrangohtGames.Tests/
RUN dotnet restore src/DrangohtGames.slnx

COPY . .

# La suite tourne dans l'image : une image ne se construit pas sur du code rouge.
RUN dotnet test --solution src/DrangohtGames.slnx --configuration $BUILD_CONFIGURATION --no-restore

RUN dotnet publish src/DrangohtGames.Web/DrangohtGames.Web.csproj \
        --configuration $BUILD_CONFIGURATION \
        --no-restore \
        --output /app

# Le répertoire de l'instantané est préparé ici : l'image finale n'a pas de shell,
# donc plus aucun `RUN` n'y est possible.
RUN mkdir -p /snapshot-dir

# --- Builds Web des jeux ------------------------------------------------------
# Auto-hébergement des jeux jouables (ADR 0007). Étape séparée : elle ne dépend que du
# manifeste, donc sa couche est réutilisée tant qu'aucune étiquette de jeu ne bouge, et
# l'étape de compilation n'a pas besoin d'un client HTTP ni d'un décompresseur.
#
# Cette étape exige le réseau vers github.com. C'est assumé : plutôt un `docker build` qui
# échoue franchement qu'une image amputée de ses jeux, déployée au vert.
FROM alpine:3.24 AS games

RUN apk add --no-cache curl jq unzip

WORKDIR /games
COPY web-builds.json scripts/fetch-web-builds.sh ./
RUN sh fetch-web-builds.sh web-builds.json /games/play

# --- Image d'exécution --------------------------------------------------------
# Image « chiseled » : ni shell, ni gestionnaire de paquets, surface d'attaque réduite.
# Variante « extra », car le site est bilingue et formate dates et montants selon la
# culture du visiteur : cela demande la globalisation ICU.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS final

WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app .

# Les jeux arrivent après le publish, donc hors du manifeste de `MapStaticAssets` : ils
# sont servis par un pipeline de fichiers statiques distinct, déclaré dans Program.cs.
COPY --from=games --chown=$APP_UID:$APP_UID /games/play ./wwwroot/play

# Point de montage de l'instantané de repli. Il appartient à l'utilisateur non privilégié :
# monté par Docker, il serait sinon possédé par root et l'application ne pourrait pas y écrire.
COPY --from=build --chown=$APP_UID:$APP_UID /snapshot-dir /var/lib/drangohtgames

ENV ASPNETCORE_HTTP_PORTS=8080 \
    # GC « workstation » : sur un petit VPS partagé, le GC serveur réserve un tas par cœur
    # pour un site dont la charge ne le justifie pas.
    DOTNET_gcServer=0 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

# Pas de HEALTHCHECK ici : l'image chiseled n'embarque ni shell, ni curl, ni wget pour
# l'exécuter. La sonde /health est interrogée de l'extérieur — par la CI après déploiement,
# et par le reverse proxy.
USER $APP_UID

ENTRYPOINT ["dotnet", "DrangohtGames.Web.dll"]
