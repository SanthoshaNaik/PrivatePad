# Stage 1: Build application using .NET 10 SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["SecureNotepad.Api/SecureNotepad.Api.csproj", "SecureNotepad.Api/"]
RUN dotnet restore "SecureNotepad.Api/SecureNotepad.Api.csproj"

# Copy remaining source code and publish release build
COPY . .
WORKDIR "/src/SecureNotepad.Api"
RUN dotnet publish "SecureNotepad.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Final lean runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render dynamically sets PORT environment variable (default 8080)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SecureNotepad.Api.dll"]

