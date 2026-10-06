# ------------------------------------------------------------------------------------
# PROG7312 - Programming 3B | Part 2: Smart-X Real-Time Processing & Device Management
# File:    SmartX.Api/Dockerfile
# Purpose: Container image for the ASP.NET Core backend API. Build context = solution root:
#            docker build -t smartx-api -f SmartX.Api/Dockerfile .
#          The WPF client is a Windows desktop app and runs on the host, not in Docker.
#
# References (Harvard):
#   - Microsoft, 2024. Containerize a .NET app with dotnet publish. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/core/docker/build-container [Accessed 25 September 2026].
#   - Microsoft, 2024. .NET container images. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/net-core-net-framework-containers/official-net-docker-images [Accessed 25 September 2026].
# ------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first (better layer caching).
COPY SmartX.Core/SmartX.Core.csproj SmartX.Core/
COPY SmartX.Api/SmartX.Api.csproj  SmartX.Api/
RUN dotnet restore SmartX.Api/SmartX.Api.csproj

# Build + publish.
COPY SmartX.Core/ SmartX.Core/
COPY SmartX.Api/  SmartX.Api/
RUN dotnet publish SmartX.Api/SmartX.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SmartX.Api.dll"]
