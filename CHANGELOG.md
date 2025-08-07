# Changelog

All notable changes to the TallaEgg project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### 🔧 Changed
- Enhanced network diagnostics and proxy support for Telegram bot
- Improved error handling in BotHandler with comprehensive logging

### 🐛 Fixed
- Resolved network connectivity issues with proxy detection
- Fixed breakpoint debugging issues by rebuilding project
- Corrected package version conflicts in test projects

## [1.0.0] - 2024-12-19

### ✅ Added
- **Complete Telegram Bot Implementation**
  - Order placement functionality with multi-step flow
  - User registration with invitation code system
  - Wallet management integration
  - Balance validation for sell orders
  - Keyboard buttons for Buy/Sell options

- **Automated Testing Framework**
  - MockBotHandler for unit testing without network dependencies
  - AutomatedTelegramClient for integration testing
  - Comprehensive test coverage for registration and order flows
  - Standalone TestRunner for reliable test execution

- **Wallet System Enhancement**
  - Wallet charging functionality with amount validation
  - Transaction recording and balance tracking
  - API endpoints for wallet operations
  - Integration with Telegram bot for balance checks

- **User Role Management System**
  - UserRole enum (RegularUser, Accountant, Admin, SuperAdmin)
  - Role-based access control for order creation
  - API endpoints for role management
  - Authorization service implementation

- **API Infrastructure**
  - CORS configuration for all APIs
  - Minimal API endpoints for Orders, Users, and Wallet
  - Entity Framework Core integration
  - Repository pattern implementation

- **Network Diagnostics**
  - Network connectivity testing utilities
  - Proxy detection and configuration
  - HTTP vs HTTPS connectivity tests
  - Bot token validation

### 🔧 Changed
- **Architecture Improvements**
  - Renamed `Wallet` class to `WalletEntity` to resolve namespace conflicts
  - Updated all API endpoints to support new wallet charging functionality
  - Enhanced BotHandler with state management for multi-step flows
  - Improved error handling in Program.cs with comprehensive diagnostics

- **Project Structure**
  - Reorganized solution structure with Clean Architecture
  - Separated concerns into Core, Application, Infrastructure, and API layers
  - Updated project references and dependencies

### 🐛 Fixed
- **Build and Compilation Issues**
  - Resolved empty IOrderRepository.cs file in Orders.Core
  - Fixed namespace conflicts between Wallet class and namespace
  - Corrected interface implementation mismatches
  - Resolved package version conflicts in test projects

- **Network and Connectivity**
  - Diagnosed and resolved network connectivity issues
  - Implemented proxy-aware bot client
  - Fixed timeout issues with improved error handling

- **Debugging Issues**
  - Resolved breakpoint debugging problems
  - Fixed source code mismatch issues
  - Improved Visual Studio debugging experience

### 🧪 Testing
- **Comprehensive Test Suite**
  - Unit tests with MockBotHandler for isolated testing
  - Integration tests with AutomatedTelegramClient
  - Network connectivity tests and diagnostics
  - Automated test runner with configuration management

- **Test Infrastructure**
  - Created separate TestRunner project for reliable execution
  - Added test configuration files and settings
  - Implemented mock implementations for external dependencies

### 📚 Documentation
- **Project Documentation**
  - Updated .gitignore with professional configuration
  - Added comprehensive inline code documentation
  - Created test configuration files and settings
  - Documented API endpoints and usage

### 🔒 Security
- **Access Control**
  - Role-based authorization for sensitive operations
  - Admin-only order creation functionality
  - User permission validation

### 🚀 Performance
- **Optimizations**
  - Improved error handling and logging
  - Enhanced network request handling
  - Optimized database queries with Entity Framework

## [0.1.0] - 2024-12-18

### ✅ Added
- Initial project setup with .NET Clean Architecture
- Basic API structure with Minimal APIs
- Entity Framework Core integration
- SQL Server database configuration
- Basic Telegram bot foundation
- Initial user and order management systems

### 📚 Documentation
- Initial project documentation
- Basic README setup
- Solution structure documentation

---

## Legend

- ✅ **Added** - New features
- 🔧 **Changed** - Changes to existing functionality  
- 🐛 **Fixed** - Bug fixes
- 🚀 **Performance** - Performance improvements
- 🔒 **Security** - Security updates
- 📚 **Documentation** - Documentation updates
- 🧪 **Testing** - Test updates
- ⚠️ **Removed** - Removed features
- 🔥 **Breaking** - Breaking changes 