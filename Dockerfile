FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY TextEnhancer.sln ./
COPY src/TextEnhancer.Api/TextEnhancer.Api.csproj src/TextEnhancer.Api/
COPY tests/TextEnhancer.Tests/TextEnhancer.Tests.csproj tests/TextEnhancer.Tests/
RUN dotnet restore src/TextEnhancer.Api/TextEnhancer.Api.csproj

COPY . .
RUN dotnet publish src/TextEnhancer.Api/TextEnhancer.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TextEnhancer.Api.dll"]
