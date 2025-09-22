# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Common Development Commands

### Build and Run
- `dotnet build` - Build the application
- `dotnet run` - Run the application locally (starts on https://localhost:7072)
- `dotnet publish -c Release` - Publish for deployment

### Development
- `dotnet watch` - Run with hot reload during development
- `dotnet restore` - Restore NuGet packages

## Architecture

SmartGallery is an ASP.NET Core Razor Pages web application for image gallery management with Azure Blob Storage integration.

### Key Components

**BlobImageService** (`Services/BlobImageService.cs`)
- Core service for Azure Blob Storage operations
- Handles image upload, listing, and SAS URL generation
- Supports both connection string and managed identity authentication
- Stores images in `/items/{guid}{ext}` and metadata in `/items/{guid}.json`
- Generates time-limited SAS URLs (7 days) for secure image access

**Configuration** (`StorageOptions.cs`)
- `UseManagedIdentity`: Toggle between managed identity and connection string auth
- `AccountName`: Storage account name (required for managed identity)
- `ConnectionString`: Storage connection string (required for connection string auth)
- `ContainerName`: Blob container name (default: "gallery")

**Pages**
- `Index.cshtml.cs`: Displays gallery of uploaded images
- `Upload.cshtml.cs`: Handles image upload with validation

### Configuration Files
- `appsettings.json`: Production configuration (uses managed identity)
- `appsettings.Development.json`: Development configuration (uses connection string)

### Dependencies
- Azure.Storage.Blobs (12.20.0) - Azure Blob Storage operations
- Azure.Identity (1.15.0) - Azure authentication
- .NET 8.0 with nullable reference types enabled

### Upload Constraints
- Maximum file size: 25MB (configured in `Program.cs`)
- Supported formats: JPEG, PNG, GIF, WebP
- Files validated by content type in `BlobImageService.IsSupportedContentType()`

### Security Notes
- Images are stored in private blob containers (no public access)
- Access controlled via time-limited SAS URLs
- Development settings contain connection strings that should not be committed to production