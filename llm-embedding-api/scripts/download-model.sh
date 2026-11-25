#!/bin/bash

# Model download script for LLM Embedding API
# This script downloads the ONNX model from Hugging Face

set -e

MODEL_DIR="/app/models"
MODEL_NAME="embeddinggemma-300m-fp16.onnx"
REPO_ID="onnx-community/embeddinggemma-300m-ONNX"
FILENAME="onnx/model_fp16.onnx"
HUGGINGFACE_TOKEN="${HUGGINGFACE_TOKEN:-}"

echo "Starting model download..."

# Create models directory if it doesn't exist
mkdir -p "$MODEL_DIR"

# Check if model already exists
if [ -f "$MODEL_DIR/$MODEL_NAME" ]; then
    echo "Model already exists at $MODEL_DIR/$MODEL_NAME"
    exit 0
fi

# Download model using Hugging Face CLI if available
if command -v huggingface-cli &> /dev/null; then
    echo "Using huggingface-cli to download model..."

    if [ -n "$HUGGINGFACE_TOKEN" ]; then
        echo "Using Hugging Face token for authentication"
        HUGGINGFACE_TOKEN="$HUGGINGFACE_TOKEN" huggingface-cli download --repo-id "$REPO_ID" --local-dir "$MODEL_DIR" --local-dir-use-symlinks False "$FILENAME"
    else
        echo "No Hugging Face token provided, trying public access"
        huggingface-cli download --repo-id "$REPO_ID" --local-dir "$MODEL_DIR" --local-dir-use-symlinks False "$FILENAME"
    fi

    # Rename the downloaded file to our expected name
    if [ -f "$MODEL_DIR/$FILENAME" ]; then
        mv "$MODEL_DIR/$FILENAME" "$MODEL_DIR/$MODEL_NAME"
    fi
else
    echo "huggingface-cli not available, trying direct download..."

    # Try direct download from Hugging Face
    URL="https://huggingface.co/$REPO_ID/resolve/main/$FILENAME"

    if [ -n "$HUGGINGFACE_TOKEN" ]; then
        echo "Using authenticated download with token"
        curl -L -H "Authorization: Bearer $HUGGINGFACE_TOKEN" "$URL" -o "$MODEL_DIR/$MODEL_NAME"
    else
        echo "Using public download"
        curl -L "$URL" -o "$MODEL_DIR/$MODEL_NAME"
    fi
fi

# Verify the download
if [ -f "$MODEL_DIR/$MODEL_NAME" ]; then
    FILE_SIZE=$(stat -c%s "$MODEL_DIR/$MODEL_NAME")
    echo "Model downloaded successfully: $MODEL_DIR/$MODEL_NAME ($FILE_SIZE bytes)"
else
    echo "Model download failed"
    exit 1
fi

echo "Model download completed successfully"