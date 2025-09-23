# TallaEgg Published Applications

This folder contains all published applications ready for deployment.

## 📁 Structure

```
publishes/
├── APIs/
│   ├── Users/           # Users API (Port 5136)
│   ├── Affiliate/       # Affiliate API (Port 60812)
│   ├── Orders/          # Orders API (Port 5140)
│   ├── Wallet/          # Wallet API (Port 60933)
│   └── TallaEgg/        # Main TallaEgg API (Port 5135)
├── TelegramBot/         # Telegram Bot Application
├── deployment-info.json # Deployment information
└── README.md           # This file
```

## 🚀 Deployment Instructions

### Prerequisites
- .NET 9 Runtime installed on target server
- SQL Server with databases created
- Windows Server or Windows VPS

### Quick Start
1. Copy the entire `publishes` folder to your server
2. Update connection strings in `appsettings.global.json` files
3. Run each application using the provided batch files

### Individual Application Startup

#### Users API
```bash
cd APIs\Users
dotnet Users.Api.dll
```

#### Affiliate API
```bash
cd APIs\Affiliate
dotnet Affiliate.Api.dll
```

#### Orders API
```bash
cd APIs\Orders
dotnet Orders.Api.dll
```

#### Wallet API
```bash
cd APIs\Wallet
dotnet Wallet.Api.dll
```

#### TallaEgg API
```bash
cd APIs\TallaEgg
dotnet TallaEgg.Api.dll
```

#### Telegram Bot
```bash
cd TelegramBot
dotnet TallaEgg.TelegramBot.Infrastructure.dll
```

## Build & Publish

Use Release builds and publish directly into this folder structure:

```powershell
# From repo root
# APIs
dotnet publish src/User/Users.Api/Users.Api.csproj -c Release -o publishes/APIs/Users
dotnet publish src/Affiliate/Affiliate.Api/Affiliate.Api.csproj -c Release -o publishes/APIs/Affiliate
dotnet publish src/Order/Orders.Api/Orders.Api.csproj -c Release -o publishes/APIs/Orders
dotnet publish src/Wallet/Wallet.Api/Wallet.Api.csproj -c Release -o publishes/APIs/Wallet
dotnet publish src/TallaEgg/TallaEgg.Api/TallaEgg.Api.csproj -c Release -o publishes/APIs/TallaEgg

# Telegram Bot
dotnet publish TelegramBot/TallaEgg.TelegramBot.Infrastructure/TallaEgg.TelegramBot.Infrastructure.csproj -c Release -o publishes/TelegramBot
```

After publishing, refresh the metadata (optional):

```powershell
$json = Get-Content publishes/deployment-info.json -Raw | ConvertFrom-Json
$json.PublishedAt = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
$json.Projects | ForEach-Object { $_.Published = $true }
$json | ConvertTo-Json -Depth 5 | Set-Content publishes/deployment-info.json -Encoding UTF8
```

## 🔧 Configuration

Each application includes:
- `appsettings.global.json` - Global configuration
- `*.dll` - Application binaries
- `*.exe` - Executable files
- `web.config` - IIS configuration (for APIs)

## 📊 Port Configuration

| Application | HTTP Port | HTTPS Port |
|-------------|-----------|------------|
| Users API | 5136 | - |
| Affiliate API | 60812 | 60811 |
| Orders API | 5140 | 7140 |
| Wallet API | 60933 | 60932 |
| TallaEgg API | 5135 | 7296 |
| Telegram Bot | 57546 | 57545 |

## 🗄️ Database Requirements

Create the following databases in SQL Server:
- `TallaEggUsers`
- `TallaEggAffiliate`
- `TallaEggOrders`
- `TallaEggWallet`
- `TallaEgg`

## 🔒 Security Notes

- Update Telegram Bot Token in configuration
- Configure proper firewall rules
- Use HTTPS in production
- Secure database connections

## 📝 Logs

Application logs are written to:
- Console output
- File logs (if configured)

## 🆘 Troubleshooting

1. **Port conflicts**: Ensure ports are available
2. **Database connection**: Verify connection strings
3. **Missing dependencies**: Install .NET 9 Runtime
4. **Permission issues**: Run as administrator if needed

## 📞 Support

For deployment issues, check:
- `deployment-info.json` for build details
- Application logs for runtime errors
- Windows Event Viewer for system errors

---
*Generated on: 2025-09-23*
*Configuration: Release*
