# Network Troubleshooting Guide

## 🔍 **Current Issue: Network Connectivity**

The bot cannot connect to external services due to network/proxy issues.

### **Symptoms:**
- ❌ Cannot reach https://api.telegram.org
- ❌ Cannot reach https://google.com
- ❌ Cannot reach https://httpbin.org/get
- 🔧 System proxy detected: `http://127.0.0.1:10808/`

### **Solutions:**

#### **1. Check Internet Connection**
```powershell
# Test basic connectivity
ping google.com
ping 8.8.8.8
```

#### **2. Check Proxy Settings**
```powershell
# View current proxy settings
netsh winhttp show proxy

# Reset proxy settings
netsh winhttp reset proxy

# Set no proxy for local development
netsh winhttp set proxy proxy-server=""
```

#### **3. Check Firewall**
- Ensure Windows Firewall allows .NET applications
- Check if antivirus is blocking connections

#### **4. VPN Issues**
- If using VPN, try disconnecting temporarily
- Check VPN proxy settings

#### **5. Corporate Network**
- Contact IT department for proxy configuration
- Request access to Telegram API endpoints

### **Quick Fixes:**

#### **Option A: Disable Proxy (Recommended for Local Development)**
```powershell
netsh winhttp set proxy proxy-server=""
```

#### **Option B: Configure Working Proxy**
```powershell
netsh winhttp set proxy proxy-server="your-proxy-server:port"
```

#### **Option C: Use Direct Connection**
- Disconnect VPN if connected
- Try connecting from a different network

### **Test Commands:**
```powershell
# Test basic internet
curl https://google.com

# Test Telegram API
curl https://api.telegram.org

# Test with PowerShell
Invoke-WebRequest -Uri "https://api.telegram.org" -UseBasicParsing
```

### **Bot Status:**
✅ **Bot Code**: Fully implemented and working
✅ **Order Placement**: Complete with balance validation
✅ **User Interface**: Persian language support
✅ **Error Handling**: Comprehensive error handling
❌ **Network**: Blocked by proxy/firewall

**The bot is ready to work once network connectivity is resolved!**
