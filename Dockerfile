FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY KellphyBot/*.csproj ./
COPY KellphyBot/NuGet.Config ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build-env /app/out .

COPY application.yml ./
COPY KellphyBot/config.json ./
ENTRYPOINT ["dotnet", "MusicBot.dll"]