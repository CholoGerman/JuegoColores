FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy csproj and restore as distinct layers
COPY ["Juego.csproj", "./"]
RUN dotnet restore "Juego.csproj"

# Copy everything else and build app
COPY . .
RUN dotnet publish "Juego.csproj" -c Release -o /app --no-restore

# Final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Set environment variables for Render
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Juego.dll"]
