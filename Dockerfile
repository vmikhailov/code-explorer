# Use .NET 10.0 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy solution and project files first to leverage Docker cache
COPY CodeExplorer.sln Directory.Build.props ./
COPY Core/CodeExplorer.Core/CodeExplorer.Core.csproj Core/CodeExplorer.Core/
COPY Parsers/CodeExplorer.Parser.CSharp/CodeExplorer.Parser.CSharp.csproj Parsers/CodeExplorer.Parser.CSharp/
COPY Parsers/CodeExplorer.Parser.Go/CodeExplorer.Parser.Go.csproj Parsers/CodeExplorer.Parser.Go/
COPY Parsers/CodeExplorer.Parser.Python/CodeExplorer.Parser.Python.csproj Parsers/CodeExplorer.Parser.Python/
COPY Parsers/CodeExplorer.Parser.TypeScript/CodeExplorer.Parser.TypeScript.csproj Parsers/CodeExplorer.Parser.TypeScript/
COPY Parsers/CodeExplorer.Parser.SQL/CodeExplorer.Parser.SQL.csproj Parsers/CodeExplorer.Parser.SQL/
COPY UI/CodeExplorer/CodeExplorer.csproj UI/CodeExplorer/
COPY Tests/CodeExplorer.Tests/CodeExplorer.Tests.csproj Tests/CodeExplorer.Tests/

# Restore dependencies
RUN dotnet restore CodeExplorer.sln

# Copy the rest of the source code
COPY . .

# Build and publish the application
WORKDIR /src/UI/CodeExplorer
RUN dotnet publish CodeExplorer.csproj -c Release -o /app/publish

# Use ASP.NET runtime for running the server
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose default SSE/REST HTTP port
EXPOSE 8085

# Set entry point
ENTRYPOINT ["dotnet", "CodeExplorer.dll"]
