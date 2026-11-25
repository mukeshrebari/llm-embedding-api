FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/LlmEmbeddingApi/LlmEmbeddingApi.csproj", "src/LlmEmbeddingApi/"]
RUN dotnet restore "src/LlmEmbeddingApi/LlmEmbeddingApi.csproj"
COPY . .
WORKDIR "/src/src/LlmEmbeddingApi"
RUN dotnet build "LlmEmbeddingApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "LlmEmbeddingApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create models directory for ONNX models
RUN mkdir -p /app/models

# Install curl for model download
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Set build arguments
ARG HUGGINGFACE_TOKEN=

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Embedding__AutoDownload=true
ENV Embedding__MaxSequenceLength=2048
ENV Embedding__EmbeddingDimension=768
ENV Embedding__HuggingFaceRepoId=onnx-community/embeddinggemma-300m-ONNX
ENV Embedding__HuggingFaceFilename=onnx/model_fp16.onnx
ENV HUGGINGFACE_TOKEN=$HUGGINGFACE_TOKEN

# Add model download script
COPY --from=build /src/scripts/download-model.sh /app/download-model.sh
RUN chmod +x /app/download-model.sh

# Download model during build (optional - can also be done at runtime)
# RUN /app/download-model.sh || echo "Model download failed - will try at runtime"

ENTRYPOINT ["dotnet", "LlmEmbeddingApi.dll"]
