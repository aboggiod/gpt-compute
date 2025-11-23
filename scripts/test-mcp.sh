#!/bin/bash

# MCP Memory Server - Test Script
# This script tests all MCP endpoints with curl

set -e

# Configuration
BASE_URL="${BASE_URL:-https://localhost:5001}"
CLIENT_ID="${CLIENT_ID:-chatgpt-client}"
CLIENT_SECRET="${CLIENT_SECRET:-\$2a\$11\$YourHashedSecretHere}"

echo "================================================"
echo "MCP Memory Server - API Test Script"
echo "================================================"
echo "Base URL: $BASE_URL"
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print step
print_step() {
    echo -e "${YELLOW}>>> $1${NC}"
}

# Function to print success
print_success() {
    echo -e "${GREEN}✓ $1${NC}"
    echo ""
}

# Function to print error
print_error() {
    echo -e "${RED}✗ $1${NC}"
    echo ""
}

# Test 1: Health Check
print_step "Test 1: Health Check"
RESPONSE=$(curl -s -k "$BASE_URL/mcp/health")
if echo "$RESPONSE" | grep -q "healthy"; then
    print_success "Health check passed"
else
    print_error "Health check failed"
    echo "$RESPONSE"
    exit 1
fi

# Test 2: Server Info
print_step "Test 2: Server Info"
RESPONSE=$(curl -s -k "$BASE_URL/mcp/info")
if echo "$RESPONSE" | grep -q "McpMemoryServer"; then
    print_success "Server info retrieved"
else
    print_error "Server info failed"
    echo "$RESPONSE"
    exit 1
fi

# Test 3: OAuth Token
print_step "Test 3: OAuth Token"
TOKEN_RESPONSE=$(curl -s -k -X POST "$BASE_URL/oauth/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=$CLIENT_ID" \
  -d "client_secret=$CLIENT_SECRET" \
  -d "scope=memory.read memory.write tools.filesystem")

ACCESS_TOKEN=$(echo "$TOKEN_RESPONSE" | grep -o '"access_token":"[^"]*' | cut -d'"' -f4)

if [ -z "$ACCESS_TOKEN" ]; then
    print_error "Failed to get access token"
    echo "$TOKEN_RESPONSE"
    exit 1
else
    print_success "Access token obtained"
fi

# Test 4: Initialize MCP Session
print_step "Test 4: Initialize MCP Session"
INIT_RESPONSE=$(curl -s -k -X POST "$BASE_URL/mcp" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
      "protocolVersion": "2024-11-05",
      "capabilities": {},
      "clientInfo": {
        "name": "TestClient",
        "version": "1.0.0"
      }
    }
  }')

SESSION_ID=$(echo "$INIT_RESPONSE" | grep -o '"sessionId":"[^"]*' | cut -d'"' -f4)

if [ -z "$SESSION_ID" ]; then
    print_error "Failed to initialize session"
    echo "$INIT_RESPONSE"
    exit 1
else
    print_success "Session initialized: $SESSION_ID"
fi

# Test 5: List Tools
print_step "Test 5: List Tools"
TOOLS_RESPONSE=$(curl -s -k -X POST "$BASE_URL/mcp" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/list",
    "params": {}
  }')

if echo "$TOOLS_RESPONSE" | grep -q "memory.read"; then
    print_success "Tools listed successfully"
else
    print_error "Failed to list tools"
    echo "$TOOLS_RESPONSE"
    exit 1
fi

# Test 6: Memory Write (Fact)
print_step "Test 6: Memory Write (Fact)"
WRITE_RESPONSE=$(curl -s -k -X POST "$BASE_URL/mcp" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "X-Session-Id: $SESSION_ID" \
  -d '{
    "jsonrpc": "2.0",
    "id": 3,
    "method": "tools/call",
    "params": {
      "name": "memory.write",
      "arguments": {
        "type": "fact",
        "content": "User prefers dark mode",
        "category": "preferences",
        "confidence": 0.95
      }
    }
  }')

if echo "$WRITE_RESPONSE" | grep -q "Fact saved"; then
    print_success "Fact written successfully"
else
    print_error "Failed to write fact"
    echo "$WRITE_RESPONSE"
    exit 1
fi

# Test 7: Memory Read
print_step "Test 7: Memory Read"
READ_RESPONSE=$(curl -s -k -X POST "$BASE_URL/mcp" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "X-Session-Id: $SESSION_ID" \
  -d '{
    "jsonrpc": "2.0",
    "id": 4,
    "method": "tools/call",
    "params": {
      "name": "memory.read",
      "arguments": {
        "type": "facts",
        "session_id": "'"$SESSION_ID"'"
      }
    }
  }')

if echo "$READ_RESPONSE" | grep -q "dark mode"; then
    print_success "Fact read successfully"
else
    print_error "Failed to read facts"
    echo "$READ_RESPONSE"
    exit 1
fi

# Test 8: Memory Search
print_step "Test 8: Memory Search"
SEARCH_RESPONSE=$(curl -s -k -X POST "$BASE_URL/mcp" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "X-Session-Id: $SESSION_ID" \
  -d '{
    "jsonrpc": "2.0",
    "id": 5,
    "method": "tools/call",
    "params": {
      "name": "memory.search",
      "arguments": {
        "query": "dark mode preferences",
        "limit": 5
      }
    }
  }')

if echo "$SEARCH_RESPONSE" | grep -q "result"; then
    print_success "Search executed successfully"
else
    print_error "Failed to search"
    echo "$SEARCH_RESPONSE"
    exit 1
fi

# Test 9: Filesystem List Directory
print_step "Test 9: Filesystem List Directory"
LIST_RESPONSE=$(curl -s -k -X POST "$BASE_URL/mcp" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -d '{
    "jsonrpc": "2.0",
    "id": 6,
    "method": "tools/call",
    "params": {
      "name": "filesystem.list_directory",
      "arguments": {
        "path": ".",
        "recursive": false
      }
    }
  }')

if echo "$LIST_RESPONSE" | grep -q "result"; then
    print_success "Directory listed successfully"
else
    print_error "Failed to list directory"
    echo "$LIST_RESPONSE"
    exit 1
fi

# Final Summary
echo "================================================"
echo -e "${GREEN}All tests passed! ✓${NC}"
echo "================================================"
echo ""
echo "Your MCP Memory Server is working correctly!"
echo "Session ID: $SESSION_ID"
echo ""
echo "Next steps:"
echo "1. Deploy to a public HTTPS endpoint"
echo "2. Configure in ChatGPT Settings → Connectors"
echo "3. Start using persistent memory in ChatGPT!"
echo ""
