#!/bin/bash

# Script to run both gRPC sample applications
# This starts the server and client in the correct order

echo "=== Simplic.OxS gRPC Sample Applications ==="
echo ""

# Build both projects first
echo "Building projects..."
dotnet build Simplic.OxS.GrpcSample.Server/Simplic.OxS.GrpcSample.Server.csproj
dotnet build Simplic.OxS.GrpcSample.Client/Simplic.OxS.GrpcSample.Client.csproj

if [ $? -ne 0 ]; then
    echo "Build failed. Please fix compilation errors."
    exit 1
fi

echo ""
echo "Starting gRPC server in background..."
cd Simplic.OxS.GrpcSample.Server
dotnet run &
SERVER_PID=$!

# Wait for server to start up
echo "Waiting for server to start up..."
sleep 5

echo ""
echo "Starting client application..."
cd ../Simplic.OxS.GrpcSample.Client
dotnet run

# Clean up: stop the server
echo ""
echo "Stopping server..."
kill $SERVER_PID

echo "Done!"