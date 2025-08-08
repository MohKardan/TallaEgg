# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
- **Telegram Bot Infrastructure DI**: Resolved OrderApiClient service resolution error in Infrastructure project
  - Added concrete class registrations for BotHandler dependencies in TallaEgg.TelegramBot.Infrastructure
  - Registered OrderApiClient, UsersApiClient, AffiliateApiClient, PriceApiClient, and WalletApiClient as concrete classes
  - Added configuration-based URL resolution with fallback defaults
  - Fixed service resolution error that was preventing Infrastructure project startup
  - Maintained both interface and concrete class registrations for flexibility
  - All BotHandler dependencies now properly resolved through DI container

- **Telegram Bot Dependency Injection**: Resolved OrderApiClient service resolution error
  - Added Microsoft.Extensions.DependencyInjection package to Telegram Bot project
  - Implemented proper DI container setup in Program.cs
  - Registered all API clients (OrderApiClient, UsersApiClient, AffiliateApiClient, PriceApiClient, WalletApiClient) as singletons
  - Registered IBotHandler interface with proper dependency injection
  - Fixed service resolution error that was preventing bot startup
  - Updated HandleUpdateAsync method to use IBotHandler interface instead of concrete class
  - All services now properly resolved through DI container

### Changed
- **Telegram Bot Architecture**: Improved service registration and dependency management
  - Moved from direct instantiation to dependency injection pattern
  - Added proper service lifetime management (singleton for all API clients)
  - Improved separation of concerns with interface-based dependencies
  - Enhanced Infrastructure project with dual registration pattern (interface + concrete)

### Technical Details
- Added `Microsoft.Extensions.DependencyInjection` package (v9.0.7) to Telegram Bot project
- Modified `Program.cs` to use `ServiceCollection` for service registration
- Updated service instantiation to use `BuildServiceProvider()` and `GetRequiredService<T>()`
- Maintained backward compatibility while improving architecture
- Added configuration-based URL resolution with fallback defaults for all API endpoints

### Fixed
- **Telegram Bot Compilation Errors**: Resolved multiple compilation errors in BotHandler
  - Fixed deconstruction variable type inference errors in HandleInvitationCodeAsync
  - Corrected method signature mismatches between BotHandler and API clients
  - Fixed variable naming conflicts in HandlePhoneNumberRequestAsync
  - Updated method calls to match actual API client signatures
  - Corrected parameter types for UseInvitationAsync method
  - Fixed UpdatePhoneAsync method call to use correct method name
  - Resolved all compilation errors while maintaining functionality

### Technical Details
- **Method Signature Corrections**:
  - Updated UseInvitationAsync call to pass invitationCode and userId correctly
  - Fixed UpdatePhoneAsync method call to use correct API client method
  - Corrected variable naming to avoid conflicts with parameter names
  - Ensured proper type inference for deconstruction variables

- **Error Handling Improvements**:
  - Enhanced error handling in invitation code processing
  - Improved user registration flow with proper validation
  - Added null checks for userId before using invitation
  - Maintained proper error messages for user feedback

### Added
- **Telegram Bot Maker/Taker Trading System**: Enhanced Telegram Bot to support comprehensive trading workflow
  - Added trading type selection (Spot/Futures) before order placement
  - Implemented asset selection with InlineKeyboardButton for available trading symbols
  - Added order confirmation step with detailed order summary
  - Enhanced OrderState with TradingType and IsConfirmed properties
  - Added new BotTexts constants for trading workflow
  - Implemented HandleTradingTypeSelectionAsync for trading type selection
  - Enhanced HandleOrderTypeSelectionAsync to work with trading types
  - Added HandleOrderConfirmationAsync for order confirmation
  - Updated OrderDto to support TradingType field
  - Enhanced TallaEgg.Api endpoint to handle new OrderDto structure
  - Added comprehensive error handling and validation throughout the workflow

### Changed
- **BotHandler**: Enhanced with complete trading workflow
  - Added TradingType selection before OrderType selection
  - Enhanced OrderState with TradingType and IsConfirmed properties
  - Improved order flow: TradingType → OrderType → Asset → Amount → Confirmation
  - Added order confirmation with detailed summary before submission
  - Enhanced callback handling for new trading workflow
  - Improved error handling and user feedback

- **OrderDto**: Updated to support new trading system
  - Added TradingType field for "Spot" or "Futures" trading
  - Updated Type field to use "Buy" or "Sell" instead of "BUY" or "SELL"
  - Enhanced compatibility with new Maker/Taker system

- **TallaEgg.Api**: Updated to handle new OrderDto structure
  - Added OrderDto class definition for Telegram Bot compatibility
  - Enhanced /api/order endpoint to convert OrderDto to CreateOrderCommand
  - Added proper error handling and validation for order submission

### Technical Details
- **Trading Workflow**: Complete user journey for order placement
  1. User clicks "📝 ثبت سفارش" button
  2. User selects trading type (💰 نقدی / 📈 آتی)
  3. User selects order type (🛒 خرید / 🛍️ فروش)
  4. User selects trading symbol from available assets
  5. User enters amount
  6. System shows order confirmation with details
  7. User confirms or cancels the order
  8. Order is submitted to the system

- **Order Confirmation**: Detailed order summary before submission
  - Shows trading symbol, order type, amount, price, and total value
  - Provides confirm and cancel options
  - Validates user balance for sell orders
  - Handles errors gracefully with user-friendly messages

- **Asset Selection**: Dynamic asset list from price API
  - Fetches available trading symbols from price service
  - Displays as InlineKeyboardButton for easy selection
  - Handles API failures gracefully
  - Provides back navigation option

- **Error Handling**: Comprehensive error management
  - Validates user existence and phone number
  - Checks balance for sell orders
  - Handles API failures with user-friendly messages
  - Provides clear error messages for each step

### Added
- **Maker/Taker Trading System**: Implemented comprehensive trading system with Maker/Taker pattern
  - Added TradingType enum (Spot, Futures) for different trading types
  - Added OrderRole enum (Maker, Taker) for order roles
  - Enhanced Order entity with trading type and role support
  - Implemented CreateMakerOrder and CreateTakerOrder factory methods
  - Added AcceptTakerOrder method for maker order management
  - Created CreateTakerOrderCommand for taker order creation
  - Enhanced OrderService with maker/taker specific operations
  - Added GetAvailableMakerOrdersAsync for listing available orders
  - Implemented AcceptTakerOrderAsync for order matching
  - Enhanced repository layer with trading type and role filtering
  - Updated database configurations with new indexes and constraints
  - Added CreateTakerOrderCommandHandler for taker order processing

### Changed
- **Order Entity**: Enhanced with trading system capabilities
  - Added TradingType and OrderRole properties
  - Added ParentOrderId for taker orders linking to maker orders
  - Implemented CreateMakerOrder and CreateTakerOrder factory methods
  - Added AcceptTakerOrder method for order matching logic
  - Enhanced business methods with role and trading type support
  - Added IsMaker(), IsTaker(), IsSpot(), IsFutures() helper methods

- **OrderService**: Extended with maker/taker functionality
  - Renamed CreateOrderAsync to CreateMakerOrderAsync
  - Added CreateTakerOrderAsync for taker order creation
  - Added GetAvailableMakerOrdersAsync for order discovery
  - Added AcceptTakerOrderAsync for order matching
  - Enhanced pagination with trading type and role filtering
  - Improved error handling and validation for trading operations

- **Repository Layer**: Enhanced with trading system support
  - Added GetOrdersByTradingTypeAsync and GetOrdersByRoleAsync
  - Added GetAvailableMakerOrdersAsync for order discovery
  - Enhanced pagination with trading type and role parameters
  - Updated database queries with new filtering capabilities
  - Added composite indexes for performance optimization

- **Database Configuration**: Updated for trading system
  - Added TradingType and OrderRole property configurations
  - Added ParentOrderId property configuration
  - Enhanced indexes for trading type and role queries
  - Added composite indexes for common trading queries
  - Optimized database schema for maker/taker operations

### Technical Details
- **Trading Types**: Support for Spot and Futures trading
  - Spot trading for immediate settlement
  - Futures trading for future settlement
  - Type-specific order management and validation

- **Order Roles**: Maker and Taker pattern implementation
  - Maker orders: Create liquidity in the market
  - Taker orders: Consume liquidity from existing orders
  - Parent-child relationship between maker and taker orders
  - Automatic order matching and settlement

- **Order Matching**: Intelligent order matching system
  - Taker orders automatically link to available maker orders
  - Price and amount validation for order matching
  - Partial and full order fulfillment support
  - Order status management for matched orders

- **Performance Optimizations**: Database and query optimizations
  - Composite indexes for common trading queries
  - Efficient filtering by trading type and role
  - Optimized pagination for large order sets
  - Enhanced query performance for order discovery

### Added
- **Orders Service Clean Architecture Refactoring**: Comprehensive refactoring of Orders service to follow Clean Architecture and Clean Code principles
  - Enhanced Order entity with proper encapsulation, validation, and business rules
  - Added OrderType and OrderStatus enums for type safety
  - Implemented domain factory method `Order.Create()` with validation
  - Added business methods: `Confirm()`, `Cancel()`, `Complete()`, `Fail()`
  - Added computed properties: `GetTotalValue()`, `IsActive()`, `CanBeCancelled()`
  - Enhanced IOrderRepository with comprehensive CRUD operations
  - Added pagination support with filtering capabilities
  - Implemented proper error handling and logging throughout the application layer
  - Created OrderService to handle business logic and provide clean interface
  - Separated IAuthorizationService and AuthorizationService into dedicated files
  - Simplified CreateOrderCommandHandler to use OrderService
  - Added comprehensive validation to CreateOrderCommand with data annotations
  - Enhanced OrderRepository with proper error handling and logging
  - Updated OrderConfigurations with proper database constraints and indexes
  - Cleaned up OrdersDbContext to only include Order-related entities
  - Removed UserRepository from Orders.Infrastructure (belongs to Users domain)

### Changed
- **Order Entity**: Completely refactored with proper encapsulation
  - Changed all properties to private setters for encapsulation
  - Added domain factory method with comprehensive validation
  - Implemented business rules and state transitions
  - Added computed properties and business methods
  - Replaced string Type with OrderType enum for type safety
  - Added OrderStatus enum for proper state management

- **Repository Pattern**: Enhanced with comprehensive operations
  - Added pagination support with filtering
  - Implemented proper error handling and logging
  - Added performance optimization with database indexes
  - Enhanced query capabilities with multiple filter options

- **Application Layer**: Improved with proper separation of concerns
  - Created OrderService to handle business logic
  - Simplified command handlers to use service layer
  - Added proper validation and error handling
  - Implemented comprehensive logging throughout

- **Infrastructure Layer**: Enhanced with proper error handling
  - Added structured logging with proper context
  - Implemented comprehensive exception handling
  - Added database performance optimizations
  - Cleaned up domain boundaries

### Technical Details
- **Domain Layer (Orders.Core)**:
  - Enhanced Order entity with business rules and validation
  - Added OrderType and OrderStatus enums for type safety
  - Implemented domain factory method with comprehensive validation
  - Added business methods for state transitions and computed properties

- **Application Layer (Orders.Application)**:
  - Created OrderService interface and implementation
  - Enhanced CreateOrderCommand with data annotations
  - Simplified CreateOrderCommandHandler to use service layer
  - Separated authorization concerns into dedicated files

- **Infrastructure Layer (Orders.Infrastructure)**:
  - Enhanced OrderRepository with comprehensive CRUD operations
  - Added proper error handling and structured logging
  - Implemented pagination and filtering capabilities
  - Updated database configurations with proper constraints and indexes

- **Clean Architecture Compliance**:
  - Proper separation of concerns between layers
  - Domain entities with business rules and validation
  - Application services handling business logic
  - Infrastructure layer with proper error handling
  - Removed cross-domain dependencies

### Fixed
- **Domain Boundaries**: Removed UserRepository from Orders.Infrastructure
  - User-related operations belong to Users domain
  - Maintained proper microservices boundaries
  - Ensured clean separation of concerns

- **Build Issues**: Resolved compilation errors
  - Fixed namespace conflicts and missing references
  - Ensured proper dependency injection setup
  - Maintained backward compatibility where possible

### Added
- **Endpoint Harmonization**: Synchronized TallaEgg.Api endpoints with Users service endpoints
  - Added comprehensive user management endpoints in TallaEgg.Api that delegate to Users microservice
  - Implemented `/api/user/register` endpoint for user registration
  - Implemented `/api/user/register-with-invitation` endpoint for invitation-based registration
  - Implemented `/api/user/update-status` endpoint for user status management
  - Implemented `/api/user/exists/{telegramId}` endpoint for user existence checks
  - Enhanced `/api/user/update-role` and `/api/users/by-role/{role}` endpoints with proper authorization
  - Updated IUsersApiClient interface with all available Users service methods
  - Enhanced UsersApiClient implementation with complete HTTP communication
  - Added proper type mapping and error handling for all user-related operations
  - Resolved namespace conflicts using using aliases for clean architecture compliance

### Changed
- **Microservices Architecture**: Refactored TallaEgg.Api to use HTTP client pattern for Users service communication
  - Removed direct dependencies on Users.Core and Users.Infrastructure from TallaEgg.Api
  - Implemented IUsersApiClient and UsersApiClient for inter-service communication
  - Updated all user-related endpoints to delegate to Users microservice
  - Added proper error handling and response mapping for all user operations
  - Maintained Clean Architecture principles with proper separation of concerns

### Fixed
- **Type Conflicts**: Resolved ambiguous reference errors between TallaEgg.Api.Clients and Users.Core namespaces
  - Added using aliases to distinguish between client and core types
  - Updated all endpoint implementations to use correct type mappings
  - Ensured proper serialization and deserialization of user data

### Technical Details
- **IUsersApiClient Interface**: Extended with complete set of user management methods
  - `RegisterUserAsync`, `RegisterUserWithInvitationAsync` for user registration
  - `UpdateUserStatusAsync`, `UpdateUserRoleAsync` for user management
  - `GetUsersByRoleAsync`, `UserExistsAsync` for user queries
  - Proper error handling and response mapping for all operations

- **UsersApiClient Implementation**: Enhanced with comprehensive HTTP communication
  - Complete implementation of all interface methods
  - Proper JSON serialization/deserialization
  - Error handling for network failures and service unavailability
  - Response mapping for different API response formats

- **Endpoint Consistency**: Ensured TallaEgg.Api endpoints match Users service exactly
  - Same URL patterns and HTTP methods
  - Consistent request/response models
  - Proper authorization checks where required
  - Comprehensive error handling and status codes

## [1.0.0] - 2024-08-04

### Added
- **Microservices Architecture Refactoring**: Refactored TallaEgg.Api to properly communicate with Users microservice
  - Removed direct dependencies on Users domain from TallaEgg.Api
  - Implemented HTTP client pattern for inter-service communication
  - Added IUsersApiClient interface and UsersApiClient implementation
  - Updated appsettings.json with UsersApiUrl configuration
  - Maintained Clean Architecture principles with proper separation of concerns

### Changed
- **Dependency Injection**: Updated TallaEgg.Api Program.cs to use HTTP client instead of direct domain dependencies
  - Removed UsersDbContext, IUserRepository, and UserService registrations
  - Added HttpClient registration for IUsersApiClient
  - Updated all user-related endpoints to use the new client pattern

### Technical Details
- **IUsersApiClient Interface**: Defines contract for communicating with Users microservice
  - `GetUserIdByInvitationCodeAsync` for invitation code lookup
  - `ValidateInvitationCodeAsync` for invitation validation
  - `GetUserByTelegramIdAsync` for user retrieval
  - `UpdateUserPhoneAsync` for phone number updates

- **UsersApiClient Implementation**: Handles HTTP communication with Users microservice
  - Uses HttpClient for making API requests
  - Implements proper error handling and response mapping
  - Configurable base URL from appsettings.json
  - JSON serialization/deserialization for request/response handling

## [0.1.0] - 2024-08-04

### Added
- **GetUserIdByInvitationCode Implementation**: Implemented the missing function in Users.Application.UserService
  - Added two-step lookup process: first in Users table, then in Invitations table
  - Updated IUserRepository interface with GetByInvitationCodeAsync method
  - Implemented GetByInvitationCodeAsync in UserRepository
  - Added proper error handling and null checks
  - Maintained Clean Architecture principles with proper separation of concerns

### Changed
- **User Entity**: Enhanced Users.Core.User with additional properties
  - Added UserRole enum with Admin, Accountant, RegularUser, Moderator values
  - Added Role property to User entity
  - Added InvitationCode property for invitation-based registration
  - Updated UserStatus enum with comprehensive status values

### Technical Details
- **UserService Implementation**: Enhanced with invitation code functionality
  - `GetUserIdByInvitationCode` method with two-step lookup process
  - `ValidateInvitationCodeAsync` method for invitation validation
  - Proper error handling and validation
  - Integration with existing user management functionality

- **Repository Layer**: Extended IUserRepository and UserRepository
  - Added GetByInvitationCodeAsync method for invitation code lookup
  - Added UpdateUserRoleAsync method for role management
  - Added GetUsersByRoleAsync method for role-based queries
  - Maintained existing functionality while adding new features

### Fixed
- **Compilation Errors**: Resolved various build issues
  - Fixed missing interface implementations
  - Resolved namespace conflicts
  - Updated package references and dependencies
  - Ensured all projects build successfully

## [0.0.1] - 2024-08-04

### Added
- **Initial Project Setup**: Created comprehensive .NET solution with Clean Architecture
  - Orders microservice with Core, Application, Infrastructure, and API layers
  - Users microservice with complete user management functionality
  - Wallet microservice with transaction and balance management
  - Telegram Bot with automated testing framework
  - Comprehensive unit and integration testing setup

### Technical Architecture
- **Clean Architecture**: Implemented proper separation of concerns
  - Core layer with domain entities and interfaces
  - Application layer with business logic and command handlers
  - Infrastructure layer with data access and external service integration
  - API layer with HTTP endpoints and request/response handling

- **Microservices Pattern**: Independent services with HTTP communication
  - Orders service for order management
  - Users service for user management and authentication
  - Wallet service for financial transactions
  - Telegram Bot for user interaction

### Testing Framework
- **Automated Testing**: Comprehensive test suite for Telegram Bot
  - Unit tests with MockBotHandler for isolated testing
  - Integration tests with AutomatedTelegramClient for end-to-end testing
  - Network connectivity tests for troubleshooting
  - Standalone test runner for easy execution

### Documentation
- **Professional Changelog**: Maintained detailed change tracking
  - Follows "Keep a Changelog" and Semantic Versioning standards
  - Comprehensive documentation of all features and changes
  - Technical details for developers and maintainers 