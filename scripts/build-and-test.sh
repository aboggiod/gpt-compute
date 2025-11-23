#!/bin/bash

# MCP Memory Server - Build and Test Script
# Runs full build, test, and verification

set -e

echo "================================================"
echo "MCP Memory Server - Build & Test"
echo "================================================"
echo ""

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

print_step() {
    echo -e "${BLUE}>>> $1${NC}"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

# Check .NET version
print_step "Checking .NET SDK version"
if ! command -v dotnet &> /dev/null; then
    print_error ".NET SDK not found. Please install .NET 8 SDK."
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
print_success ".NET SDK version: $DOTNET_VERSION"
echo ""

# Clean previous builds
print_step "Cleaning previous builds"
dotnet clean --nologo
find . -type d -name "bin" -o -name "obj" | xargs rm -rf
rm -f *.db *.db-shm *.db-wal
print_success "Clean complete"
echo ""

# Restore dependencies
print_step "Restoring NuGet packages"
dotnet restore --nologo
print_success "Dependencies restored"
echo ""

# Build solution
print_step "Building solution"
if dotnet build --nologo --configuration Release; then
    print_success "Build successful"
    echo ""
else
    print_error "Build failed"
    exit 1
fi

# Run unit tests
print_step "Running unit tests"
if dotnet test --nologo --configuration Release --logger "console;verbosity=normal"; then
    print_success "All tests passed"
    echo ""
else
    print_error "Tests failed"
    exit 1
fi

# Generate test coverage report (optional)
if command -v reportgenerator &> /dev/null; then
    print_step "Generating coverage report"
    dotnet test --nologo /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput=./coverage/
    print_success "Coverage report generated"
    echo ""
fi

# Build summary
echo "================================================"
echo -e "${GREEN}Build and Test Complete! ✓${NC}"
echo "================================================"
echo ""
echo "Build artifacts:"
echo "  - Release build: src/McpMemoryServer/bin/Release/net8.0/"
echo "  - Test results: tests/McpMemoryServer.Tests/TestResults/"
echo ""
echo "Next steps:"
echo "  1. Run the server: cd src/McpMemoryServer && dotnet run"
echo "  2. Test endpoints: ./scripts/test-mcp.sh"
echo "  3. Deploy to production"
echo ""
