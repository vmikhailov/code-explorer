# Use .NET 10.0 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first to leverage Docker cache
COPY CodeExplorer.slnx Directory.Build.props ./
COPY src/Core/CodeExplorer.Core/CodeExplorer.Core.csproj src/Core/CodeExplorer.Core/
COPY src/Parsers/CodeExplorer.Parser.CSharp/CodeExplorer.Parser.CSharp.csproj src/Parsers/CodeExplorer.Parser.CSharp/
COPY src/Parsers/CodeExplorer.Parser.Go/CodeExplorer.Parser.Go.csproj src/Parsers/CodeExplorer.Parser.Go/
COPY src/Parsers/CodeExplorer.Parser.Python/CodeExplorer.Parser.Python.csproj src/Parsers/CodeExplorer.Parser.Python/
COPY src/Parsers/CodeExplorer.Parser.TypeScript/CodeExplorer.Parser.TypeScript.csproj src/Parsers/CodeExplorer.Parser.TypeScript/
COPY src/Parsers/CodeExplorer.Parser.SQL/CodeExplorer.Parser.SQL.csproj src/Parsers/CodeExplorer.Parser.SQL/
COPY src/UI/CodeExplorer/CodeExplorer.csproj src/UI/CodeExplorer/
COPY tests/CodeExplorer.Tests/CodeExplorer.Tests.csproj tests/CodeExplorer.Tests/
COPY src/Tools/CodeExplorer.OntologyGen/OntologyGen.csproj src/Tools/CodeExplorer.OntologyGen/

# Restore dependencies
RUN dotnet restore CodeExplorer.slnx

# Copy the rest of the source code
COPY . .

# Build and publish the application
WORKDIR /src/src/UI/CodeExplorer
RUN dotnet publish CodeExplorer.csproj -c Release -o /app/publish -p:BuildingInsideDocker=true

# Clean up unused platform runtimes to significantly shrink the final image
RUN rm -rf /app/publish/runtimes/win* \
           /app/publish/runtimes/osx* \
           /app/publish/runtimes/linux-x86 \
           /app/publish/runtimes/linux-arm

# Use ASP.NET runtime for running the server
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Resolve Docker Desktop VM symlinks for macOS/Windows path resolution
RUN ln -s /host/host_mnt /host_mnt

# Expose default SSE/REST HTTP port
EXPOSE 8085

# Set entry point
ENTRYPOINT ["dotnet", "CodeExplorer.dll"]
