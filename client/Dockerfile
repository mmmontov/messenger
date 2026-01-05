FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src
COPY client /src/client

RUN dotnet restore /src/client/Messenger.Client/Messenger.Client.csproj
RUN dotnet publish /src/client/Messenger.Client/Messenger.Client.csproj -c Release -o /out

# NOTE: Avalonia desktop GUI не запускается “нормально” в контейнере без GUI-среды.
# Этот образ предназначен для сборки артефактов клиента.


