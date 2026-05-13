#!/bin/bash
set -e

echo "Building Local AI Assistant..."

# Build frontend
echo "Building frontend..."
cd frontend
npm install
npm run build
cd ..

# Copy frontend dist to cmd
mkdir -p cmd/dist
cp -r frontend/dist/* cmd/dist/

# Build Windows executable
echo "Building Windows executable..."
GOOS=windows go build -o build/local-ai-assistant.exe ./cmd

echo "Build complete! Output: build/local-ai-assistant.exe"
echo "Size: $(du -h build/local-ai-assistant.exe | cut -f1)"
