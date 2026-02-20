# Build stage – always runs on the host platform for speed
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# Restore dependencies first for better layer caching
COPY ["OctopusCosyAnalyser.ApiService/OctopusCosyAnalyser.ApiService.csproj", "OctopusCosyAnalyser.ApiService/"]
COPY ["OctopusCosyAnalyser.ServiceDefaults/OctopusCosyAnalyser.ServiceDefaults.csproj", "OctopusCosyAnalyser.ServiceDefaults/"]
COPY ["OctopusCosyAnalyser.Shared/OctopusCosyAnalyser.Shared.csproj", "OctopusCosyAnalyser.Shared/"]
RUN dotnet restore "OctopusCosyAnalyser.ApiService/OctopusCosyAnalyser.ApiService.csproj" \
    -r "linux-$(echo "$TARGETARCH" | sed 's/amd64/x64/')"

# Copy remaining source and publish
COPY . .
RUN dotnet publish "OctopusCosyAnalyser.ApiService/OctopusCosyAnalyser.ApiService.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    --os linux \
    --arch "$(echo "$TARGETARCH" | sed 's/amd64/x64/')"

# Runtime stage – targets the correct platform
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OctopusCosyAnalyser.ApiService.dll"]
