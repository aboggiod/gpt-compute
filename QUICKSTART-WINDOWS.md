# Quick Start Guide for Windows

## Prerequisites

1. Install .NET 8 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0
2. Verify installation:
   ```cmd
   dotnet --version
   ```
   Should show: `8.0.x`

## Step 1: Quick Build Test

Open PowerShell or Command Prompt in the project root and run:

```cmd
cd scripts
quick-build-test.cmd
```

This will verify the solution builds correctly.

## Step 2: Full Build and Test

```cmd
build-and-test.cmd
```

This runs:
- ✓ Clean
- ✓ Restore packages
- ✓ Build solution
- ✓ Run all 95 unit tests

**Expected output:** All tests pass ✓

## Step 3: Run the Server

```cmd
cd ..\src\McpMemoryServer
dotnet run
```

The server will start on `https://localhost:5001`

Open your browser to see the Swagger UI: https://localhost:5001

## Step 4: Test the OAuth Endpoint

Open a new terminal and test getting a token:

```powershell
$body = @{
    grant_type = "client_credentials"
    client_id = "chatgpt-client"
    client_secret = "`$2a`$11`$YourHashedSecretHere"
    scope = "memory.read memory.write tools.filesystem"
}

$response = Invoke-RestMethod -Uri "https://localhost:5001/oauth/token" `
    -Method Post `
    -Body $body `
    -ContentType "application/x-www-form-urlencoded" `
    -SkipCertificateCheck

$token = $response.access_token
Write-Host "Token: $token"
```

## Step 5: Test the MCP Endpoint

```powershell
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$body = @{
    jsonrpc = "2.0"
    id = 1
    method = "initialize"
    params = @{
        protocolVersion = "2024-11-05"
        clientInfo = @{
            name = "TestClient"
            version = "1.0.0"
        }
        capabilities = @{}
    }
} | ConvertTo-Json -Depth 10

$response = Invoke-RestMethod -Uri "https://localhost:5001/mcp" `
    -Method Post `
    -Headers $headers `
    -Body $body `
    -SkipCertificateCheck

$response | ConvertTo-Json -Depth 10
```

## Step 6: Deploy for ChatGPT

### Option A: Use ngrok (easiest)

1. Download ngrok: https://ngrok.com/download
2. Run:
   ```cmd
   ngrok http https://localhost:5001
   ```
3. Copy the HTTPS URL (e.g., `https://abc123.ngrok.io`)

### Option B: Deploy to Azure/AWS

Deploy the `src/McpMemoryServer` folder to your cloud provider.

## Step 7: Configure in ChatGPT

1. Go to ChatGPT → Settings → Connectors → Create
2. Fill in:
   - **Name:** My Memory Server
   - **Description:** Persistent memory across sessions
   - **Connector URL:** `https://your-ngrok-url.com/mcp`
3. OAuth Configuration:
   - **Token URL:** `https://your-ngrok-url.com/oauth/token`
   - **Client ID:** `chatgpt-client`
   - **Client Secret:** `$2a$11$YourHashedSecretHere`
   - **Scopes:** `memory.read memory.write tools.filesystem`

## Troubleshooting

### "dotnet not found"
- Install .NET 8 SDK
- Restart terminal after installation

### Build fails with package errors
```cmd
dotnet restore --force
dotnet clean
dotnet build
```

### Tests fail
```cmd
cd tests\McpMemoryServer.Tests
dotnet test --logger "console;verbosity=detailed"
```

### Port already in use
Edit `src/McpMemoryServer/appsettings.json` and change the port:
```json
"Urls": "https://localhost:5002"
```

## Common Issues

**Q: Line ending errors in bash scripts?**
A: Use the Windows batch files (`.cmd`) instead of bash scripts (`.sh`)

**Q: Certificate errors?**
A: In development, use `-SkipCertificateCheck` in PowerShell or trust the dev certificate:
```cmd
dotnet dev-certs https --trust
```

**Q: Database locked errors?**
A: Stop the server and delete `*.db` files, then restart

## Next Steps

- See README.md for full documentation
- All MCP tools documented with examples
- Production deployment guide
- Security checklist
