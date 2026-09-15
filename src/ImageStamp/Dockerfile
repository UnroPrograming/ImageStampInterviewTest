# Build context is the repository root:
#   docker build -f src/ImageStamp/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY NuGet.config ./
COPY src/ImageStamp/ImageStamp.csproj src/ImageStamp/

RUN dotnet restore src/ImageStamp/ImageStamp.csproj

COPY src/ src/

RUN dotnet publish src/ImageStamp/ImageStamp.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ImageStamp.dll"]
