# LLM Embedding API

This project is a .NET 9 C# 13 API that exposes the `embeddinggemma` model via an HTTP API at the `/api/embed` endpoint. It is designed to run in a non-GPU container, utilizing RAM and CPU for processing.

## Features

- **Embedding API**: Provides an endpoint to generate embeddings using the `embeddinggemma` model.
- **Modular Architecture**: Organized into controllers, models, services, and configuration for maintainability.
- **Unit Tests**: Comprehensive tests for controllers and services to ensure reliability.

## Project Structure

```
llm-embedding-api
├── src
│   ├── LlmEmbeddingApi
│   │   ├── Controllers          # Contains API controllers
│   │   ├── Models               # Data models for requests and responses
│   │   ├── Services             # Business logic for embedding
│   │   ├── Configuration         # Configuration settings
│   │   ├── Program.cs           # Entry point of the application
│   │   └── LlmEmbeddingApi.csproj # Project file
├── tests
│   └── LlmEmbeddingApi.Tests    # Unit tests for the API
├── docker
│   ├── Dockerfile                # Docker instructions for building the image
│   └── .dockerignore             # Files to ignore in Docker builds
├── appsettings.json              # Application configuration
├── appsettings.Development.json   # Development-specific configuration
├── LlmEmbeddingApi.sln           # Solution file
└── README.md                     # Project documentation
```

## Getting Started

### Prerequisites

- .NET 9 SDK
- Docker (for containerization)

### Setup

1. Clone the repository:
   ```
   git clone <repository-url>
   cd llm-embedding-api
   ```

2. Restore the dependencies:
   ```
   dotnet restore
   ```

3. Build the project:
   ```
   dotnet build
   ```

### Running the API

To run the API locally, use the following command:
```
dotnet run --project src/LlmEmbeddingApi/LlmEmbeddingApi.csproj
```

### Docker

To build and run the API in a Docker container, use the following commands:

1. Build the Docker image:
   ```
   docker build -t llm-embedding-api ./docker
   ```

2. Run the Docker container:
   ```
   docker run -p 5000:80 llm-embedding-api
   ```

### API Usage

Once the API is running, you can access the embedding endpoint at:
```
POST http://localhost:5000/api/embed
```

### Contributing

Contributions are welcome! Please open an issue or submit a pull request for any enhancements or bug fixes.

### License

This project is licensed under the MIT License. See the LICENSE file for details.