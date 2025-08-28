# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0] - 2025-08-28

### Added
- **💰 Wallet Integration for Order Balance Validation**: Complete integration with Wallet service for balance validation and freeze transactions
  - Added `IWalletApiClient` interface for communication with Wallet service
  - Implemented `WalletApiClient` with full HTTP client functionality
  - Added balance validation before order placement with `ValidateBalanceAsync`
  - Implemented balance freeze mechanism with `LockBalanceAsync` for order commitment
  - Added balance unlock functionality with `UnlockBalanceAsync` for order cancellation
  - Added asset-specific balance calculation logic for Buy/Sell orders
  - Integrated rollback mechanism for failed order creation

- **🔒 Advanced Order Balance Management**: Enhanced order lifecycle with proper balance handling
  - Buy orders now require USDT balance validation (amount × price)
  - Sell orders now require asset balance validation (amount)
  - Balance is frozen (locked) immediately upon order creation
  - Balance is automatically unlocked when orders are cancelled
  - Added comprehensive error handling for insufficient balance scenarios
  - Implemented proper transaction rollback on order creation failures

- **🔗 Wallet Service Endpoints Enhancement**: Extended Wallet service with new balance management endpoints
  - Added `POST /api/wallet/unlockBalance` endpoint for releasing frozen funds
  - Enhanced existing balance validation endpoints for order integration
  - Added proper error handling and response formatting for all endpoints

### Changed
- **📈 Enhanced OrderService Architecture**: Complete refactoring of order creation workflow
  - Updated `CreateMakerOrderAsync` with full wallet integration
  - Added `CalculateRequiredBalance` helper method for asset-specific calculations
  - Enhanced `CancelOrderAsync` with automatic balance unlock functionality
  - Added comprehensive logging for all wallet-related operations
  - Improved error handling with user-friendly Persian messages

- **🔧 Dependency Injection Updates**: Enhanced service configuration and dependencies
  - Added HttpClient configuration for WalletApiClient communication
  - Registered IWalletApiClient interface with proper DI lifetime management
  - Added configuration support for WalletApiUrl in appsettings.json
  - Updated Orders.Api Program.cs with proper service registrations

- **⚙️ Configuration Management**: Enhanced application configuration
  - Added WalletApiUrl configuration to appsettings.json
  - Set default Wallet service URL to http://localhost:60933
  - Added proper configuration binding for HTTP client base address

### Fixed
- **🔄 Order Lifecycle Management**: Resolved balance consistency issues
  - Fixed race conditions between order creation and balance updates
  - Implemented proper rollback mechanism for failed order scenarios
  - Added validation to prevent orders with insufficient balance
  - Fixed memory leaks in unused exception variables

- **🌐 HTTP Client Configuration**: Resolved configuration binding issues
  - Fixed IConfiguration.GetValue usage in WalletApiClient
  - Updated to use IConfiguration indexer for better compatibility
  - Added proper error handling for service unavailability scenarios

### Technical Details
- **🏗️ Architecture**: Service-oriented architecture with proper separation of concerns
  - Orders service communicates with Wallet service via HTTP APIs
  - Implemented proper client-server pattern for microservices communication
  - Added comprehensive error handling and retry mechanisms
  - Maintained transaction consistency across service boundaries

- **💡 Balance Logic**: Sophisticated balance calculation and management
  - **Buy Orders**: Require base currency (USDT) = quantity × price
  - **Sell Orders**: Require actual asset = quantity amount
  - **Freeze Process**: Immediate balance lock upon order validation success
  - **Unlock Process**: Automatic balance release on order cancellation or failure

- **🔐 Transaction Safety**: Comprehensive transaction management
  - Atomic balance validation and freeze operations
  - Proper rollback mechanisms for partial failures
  - Consistent error handling across all balance operations
  - Detailed logging for audit trail and debugging

- **📊 Error Handling**: Enhanced error management and user feedback
  - Persian error messages for better user experience
  - Detailed logging for debugging and monitoring
  - Proper HTTP status codes for different error scenarios
  - Graceful degradation for service unavailability

### Integration Points
- **Wallet Service Communication**: Full HTTP-based integration
  - GET /api/wallet/balance/{userId}/{asset} - Balance inquiry
  - POST /api/wallet/lockBalance - Freeze funds for orders
  - POST /api/wallet/unlockBalance - Release frozen funds
  - POST /api/wallet/market/validate-balance - Pre-validation

### Testing
- **Unit Tests**: All existing tests pass with new functionality
- **Integration Tests**: Wallet service communication verified
- **Balance Scenarios**: Tested insufficient balance handling
- **Rollback Mechanisms**: Verified proper error recovery
- **Service Communication**: HTTP client reliability confirmed

### Breaking Changes
- Orders now require running Wallet service for balance validation
- Order creation will fail if Wallet service is unavailable
- Balance must be sufficient before any order can be created

### Migration Guide
- Ensure Wallet service is running on configured port (default: 60933)
- Update any direct order creation calls to handle balance validation errors
- Configure WalletApiUrl in appsettings.json if using different port

## [2.0.0] - 2025-08-28

### Added
- **🔒 Thread-Safe Database Lock Implementation**: Implemented comprehensive Database Pessimistic Locking for Orders.Api
  - Added `OrderMatchingRepository` with database-level locking using UPDLOCK and READPAST hints
  - Implemented `GetBuyOrdersWithLockAsync` and `GetSellOrdersWithLockAsync` with proper ORDER BY clauses
  - Added LINQ-based queries for SQL Server compatibility and enum handling
  - Integrated SemaphoreSlim for application-level concurrency control
  - Added proper price-time priority ordering (DESC for Buy, ASC for Sell)

- **⚡ Complete Matching Engine Rewrite**: Redesigned matching engine as thread-safe BackgroundService
  - Complete rewrite of `MatchingEngineService` with proper thread safety
  - Fixed critical race condition issues in original matching logic
  - Implemented per-asset locking using `SemaphoreSlim` for concurrent processing
  - Added proper price compatibility logic (>= instead of incorrect == comparison)
  - Enhanced partial fill handling with proper order status transitions
  - Added continuous background processing every 5 seconds

- **🛡️ Race Condition Prevention**: Eliminated critical race conditions in order matching
  - Implemented database pessimistic locking to prevent concurrent access
  - Added proper thread synchronization with SemaphoreSlim
  - Fixed order status updates to include Pending orders in active queries
  - Added atomic order processing with database transactions
  - Eliminated duplicate trade creation and inconsistent order states

### Changed
- **📈 Enhanced Order Repository**: Fixed GetActiveOrdersAsync to include all matchable orders
  - Updated WHERE clause to include Pending, Confirmed, and PartiallyFilled orders
  - Fixed missing Pending orders from active order matching
  - Added proper order status filtering for matching engine compatibility

- **🔧 SQL Server Compatibility**: Switched from Raw SQL to LINQ for better enum handling
  - Replaced problematic Raw SQL queries with LINQ expressions
  - Fixed SQL Server enum conversion errors (Cannot convert varchar to int)
  - Added proper OrderType enum handling in database queries
  - Enhanced query performance with Entity Framework Core optimization

- **⚙️ Dependency Injection**: Updated Program.cs with new thread-safe components
  - Added `AddScoped<OrderMatchingRepository>()` registration
  - Maintained existing service registrations for backward compatibility
  - Enhanced DI container configuration for new locking infrastructure

### Fixed
- **🚨 Critical Price Matching Bug**: Fixed incorrect price comparison logic
  - Changed from `buyOrder.Price == sellOrder.Price` to `buyOrder.Price >= sellOrder.Price`
  - Enables proper market order matching where buy price meets or exceeds sell price
  - Fixed trading logic that was preventing valid order matches

- **⚠️ SQL Query Compatibility**: Resolved Entity Framework Core SQL translation errors
  - Fixed "Cannot convert varchar 'Buy' to int" error in Raw SQL queries
  - Replaced integer enum casting with proper LINQ enum handling
  - Enhanced SQL Server compatibility for enum-based filtering

- **🔄 Order Status Management**: Fixed incomplete order status handling
  - Added Pending orders to active order queries for proper matching
  - Fixed order status transitions during partial fills
  - Enhanced order lifecycle management with proper status updates

### Technical Details
- **🏗️ Architecture**: Thread-safe Clean Architecture implementation
  - Background Service pattern for continuous order matching
  - Repository pattern with database locking capabilities
  - Dependency injection with proper service lifetimes
  - Clean separation of concerns with domain-driven design

- **🔒 Locking Strategy**: Multi-layer concurrency control
  - **Database Level**: UPDLOCK and READPAST hints for row-level locking
  - **Application Level**: SemaphoreSlim for per-asset synchronization
  - **Transaction Level**: Atomic operations with Entity Framework transactions
  - **Query Level**: Proper ORDER BY for consistent locking order

- **📊 Performance Optimizations**: Enhanced matching engine performance
  - Asset-based processing for parallel order matching
  - Efficient LINQ queries with proper indexing
  - Reduced database round-trips with batch operations
  - Optimized SQL generation with Entity Framework Core

- **🧪 Testing**: Comprehensive thread-safety validation
  - Unit tests for matching logic with thread safety scenarios
  - Integration tests with database locking verification
  - Performance testing under concurrent load
  - Successfully validated thread-safe operation with build and runtime testing

### Breaking Changes
- **API Compatibility**: Maintained backward compatibility for all existing endpoints
- **Database Schema**: No breaking changes to existing table structures
- **Service Interface**: All existing service methods remain unchanged

### Migration Guide
- **Deployment**: No manual migration required - services are backward compatible
- **Configuration**: No configuration changes needed
- **Dependencies**: All existing dependencies maintained

## [1.9.0] - 2025-08-25

### Added
- **Trading System Implementation**
  - Added `RemainingAmount` field to `Order` entity for tracking remaining quantity
  - Implemented `Trade` entity with comprehensive trading fields
  - Added `Transaction` entity in Wallet service with detailed transaction tracking
  - Implemented **Matching Engine** as Background Service in ASP.NET Core
  - Added Persian display names to all enums using `[Description]` attributes
  - Added `Detail` field (nvarchar(max)) to `Transaction` table for JSON data storage
  - Added new status-based filtering endpoints:
    - `GET /api/orders/pending`
    - `GET /api/orders/confirmed`
    - `GET /api/orders/partially`
    - `GET /api/orders/completed`
  - Added Swagger documentation for all new endpoints
  - Implemented proper XML comments for API documentation

### Changed
- **Order Management**
  - Modified `Order` entity to use `RemainingAmount` for quantity tracking
  - Updated factory methods to initialize `RemainingAmount` with initial order amount
  - Renamed `UpdateAmount` to `UpdateRemainingAmount` with validation logic
  - Updated `GetTotalValue()` to use `RemainingAmount * Price`
  - Modified `AcceptTakerOrder` to use `RemainingAmount` for checks and deductions

- **Matching Engine Logic**
  - Implemented Price-Time Priority matching algorithm
  - Added exact price match requirement between buy and sell orders
  - Implemented partial fills support with proper status updates
  - Added comprehensive error handling for zero trade quantities
  - Fixed order status updates to correctly transition between states

- **API Endpoints**
  - Added new status-based filtering endpoints with Swagger documentation
  - Implemented proper XML comments for API documentation
  - Enhanced error handling and validation throughout

- **Database Schema**
  - Added `RemainingAmount` column to `Orders` table with precision(18,2)
  - Created migration to update existing orders' `RemainingAmount` values
  - Added `Detail` field to `Transaction` table for flexible data storage

- **OrderStatus Enum**
  - Reordered enum values to match database expectations:
    - `Pending = 0`
    - `Confirmed = 1`
    - `Partially = 2`
    - `Completed = 3`
    - `Cancelled = 4`
    - `Failed = 5`

### Fixed
- **Matching Engine Issues**
  - Fixed `ArgumentException: Quantity must be greater than zero` by adding trade amount validation
  - Fixed order status updates not working due to missing `UpdateStatus` method
  - Fixed `OrderStatus.Partially` not being handled in `OrderRepository.UpdateStatusAsync`
  - Fixed out-of-bounds index errors in matching algorithm
  - Fixed status mapping between enum values and database values

- **API Response Issues**
  - Fixed incorrect status values in API responses due to enum mapping
  - Fixed missing `GetOrdersByStatusAsync` method implementation
  - Fixed EF Core logging verbosity by configuring to show only warnings and errors

### Technical Details
- **Architecture**: Clean Architecture with microservices pattern
- **Database**: Entity Framework Core with SQL Server
- **Background Service**: `IHostedService` implementation for matching engine
- **API**: RESTful endpoints with Swagger documentation
- **Validation**: Comprehensive business rule validation in domain entities
- **Error Handling**: Proper exception handling with meaningful error messages

### Testing
- Successfully tested order creation, confirmation, and matching
- Verified partial fills work correctly with proper status transitions
- Confirmed trade records are created in database
- Validated API responses show correct status values
- Tested background service runs continuously without errors

### Files Modified
- `src/Orders.Core/Order.cs` - Added RemainingAmount field and UpdateStatus method
- `src/Orders.Application/Services/MatchingEngineService.cs` - Implemented matching logic
- `src/Orders.Infrastructure/OrderRepository.cs` - Added Partially status handling
- `src/Orders.Api/Program.cs` - Added new endpoints and EF Core logging configuration
- `src/TallaEgg.Core/Enums/Order/OrderStatus.cs` - Reordered enum values
- `src/Orders.Infrastructure/Configurations/OrderConfigurations.cs` - Added RemainingAmount configuration
- Database migrations for schema changes

### Next Steps
- Implement Trade API endpoints for retrieving trade history
- Add real-time notifications for order status changes
- Implement fee calculation and wallet integration
- Add order book depth and market data endpoints
- Implement advanced order types (stop-loss, take-profit)

## [1.8.0] - 2024-12-19

### Added
- **Matching Engine Implementation**: Implemented comprehensive trading matching engine as Background Service
  - Created IMatchingEngine interface for matching engine operations
  - Implemented MatchingEngineService as BackgroundService with automatic order processing
  - Added automatic order matching with price-time priority algorithm
  - Implemented trade creation and order status management
  - Added fee calculation and management (0.1% default fee rate)
  - Enhanced Order entity with UpdateAmount() method for partial fills
  - Integrated matching engine with existing Order and Trade repositories
  - Added comprehensive logging for all matching operations
  - Implemented cancellation token support for graceful shutdown

### Technical Details
- **Matching Engine Service**: High-performance background service for order matching
  - **Background Service**: Implements IHostedService for automatic startup/shutdown
  - **Price-Time Priority**: Orders matched by best price first, then by earliest timestamp
  - **Automatic Processing**: Processes all pending orders every 1 second
  - **Asset-Based Grouping**: Efficiently groups orders by asset for faster matching
  - **Partial Fills**: Supports partial order fills with remaining amount tracking
  - **Trade Creation**: Automatically creates Trade entities for matched orders
  - **Status Management**: Updates order statuses (Completed, Partially) based on fills
  - **Fee Calculation**: Calculates and applies trading fees for both buyer and seller

- **Order Processing Logic**: Comprehensive order matching algorithm
  - **Price Compatibility**: Ensures buy orders only match with sell orders at compatible prices
  - **Amount Calculation**: Calculates trade amounts based on minimum of both orders
  - **Order Updates**: Updates order amounts and statuses after each trade
  - **Batch Processing**: Processes multiple orders efficiently in single database transactions
  - **Error Handling**: Comprehensive error handling with detailed logging

- **Integration**: Seamless integration with existing architecture
  - **Dependency Injection**: Properly registered as scoped service and hosted service
  - **Repository Pattern**: Uses existing IOrderRepository and ITradeRepository
  - **Domain Entities**: Works with existing Order and Trade domain entities
  - **Database Transactions**: Maintains data consistency across order and trade updates
  - **Configuration**: Configurable processing interval and fee rates

### Changed
- **Order Entity**: Enhanced with amount update capability
  - Added UpdateAmount() method for partial order fills
  - Added validation for negative amounts and completed orders
  - Maintains UpdatedAt timestamp for audit trail

### Architecture Benefits
- **Low Coupling**: Minimal dependencies on other services
- **High Performance**: Efficient algorithms for order matching
- **Scalability**: Designed for future microservice extraction
- **Reliability**: Comprehensive error handling and logging
- **Maintainability**: Clean separation of concerns and modular design

## [1.7.0] - 2024-12-19

### Added
- **Enhanced Trade Table Implementation in Order Service**: Completely redesigned Trade entity with comprehensive trading features
  - Redesigned Trade entity with new structure: BuyOrderId, SellOrderId, Symbol, Price, Quantity, QuoteQuantity, BuyerUserId, SellerUserId, FeeBuyer, FeeSeller, CreatedAt, UpdatedAt
  - Added navigation properties to BuyOrder and SellOrder for proper relationships
  - Enhanced Create() factory method with comprehensive validation for all new fields
  - Added UpdateFees() method for updating transaction fees
  - Added business methods: GetTotalValue(), GetTotalQuoteValue(), GetTotalFees()
  - Updated ITradeRepository interface with new query methods for BuyOrderId, SellOrderId, Symbol, BuyerUserId, SellerUserId
  - Implemented TradeRepository with all new CRUD operations and pagination support
  - Enhanced TradeConfiguration with proper foreign key relationships and comprehensive indexes
  - Added proper validation for all fields including fee validation (non-negative values)
  - Implemented pagination with multiple filtering options for comprehensive trade analysis

### Technical Details
- **Trade Entity**: Complete redesign for exchange trading operations
  - Foreign key relationships with Order entity (BuyOrder and SellOrder)
  - Comprehensive validation for all trading parameters
  - Business methods for value calculations and fee management
  - Proper encapsulation with private setters and domain methods

- **Database Schema**: Optimized for trading operations
  - Foreign key relationships with Restrict delete behavior
  - Precision settings for decimal fields (18, 8)
  - Comprehensive indexes for performance optimization
  - Symbol field with max length constraint (20 characters)

- **Repository Pattern**: Complete CRUD operations with advanced querying
  - Query methods for all major trading scenarios
  - Pagination support with multiple filtering options
  - Performance-optimized database queries
  - Proper error handling and logging

## [1.6.0] - 2024-12-19

### Added
- **Transaction Detail Field Enhancement**: Added Detail field to Transaction entity for JSON data storage
  - Added Detail property to Transaction entity for storing additional transaction information as JSON
  - Added UpdateDetail() method for updating transaction details
  - Enhanced Create() factory method to accept detail parameter
  - Configured Detail field as nvarchar(max) in database for unlimited JSON storage
  - Added proper validation and error handling for detail updates

## [1.5.0] - 2024-12-19

### Added
- **Transaction Table Implementation in Wallet Service**: Implemented comprehensive Transaction entity and management system
  - Created Transaction entity in Wallet.Core with properties: Id, WalletId, Amount, Currency, Type, Status, ReferenceId, Description, CreatedAt, UpdatedAt
  - Added domain factory method `Transaction.Create()` with comprehensive validation
  - Implemented ITransactionRepository interface with full CRUD operations and pagination support
  - Created TransactionRepository implementation with proper error handling and logging
  - Updated WalletDbContext to include Transaction entity with proper foreign key relationships
  - Created ITransactionService and TransactionService for business logic handling
  - Enhanced TransactionType enum with new types: Freeze, Unfreeze, Trade
  - Updated TransactionStatus enum (Cancelled → Canceled for consistency)
  - Added comprehensive business operations: Deposit, Withdraw, Freeze, Unfreeze
  - Implemented transaction status management: Complete, Fail, Cancel
  - Added pagination support with filtering by WalletId, UserId, Currency, Type, Status
  - Enhanced database schema with performance-optimized indexes and foreign key constraints

### Technical Details
- **Transaction Entity**: Domain-driven design with proper encapsulation
  - Private setters for encapsulation and data integrity
  - Domain factory method with comprehensive validation
  - Business methods for status transitions: Complete(), Fail(), Cancel()
  - Helper methods: IsPending(), IsCompleted(), IsFailed(), IsCancelled(), CanBeModified()
  - Navigation property to Wallet entity for proper relationships

- **Business Operations**: Complete transaction lifecycle management
  - **Deposit**: Credit wallet balance with transaction record
  - **Withdraw**: Debit wallet balance with balance validation
  - **Freeze**: Lock balance for pending orders with available balance check
  - **Unfreeze**: Release locked balance with locked balance validation
  - **Status Management**: Complete, Fail, Cancel with proper state transitions

- **Repository Pattern**: Complete CRUD operations with performance optimization
  - Create, Read, Update, Delete operations with proper error handling
  - Pagination support with multiple filtering options
  - Date range queries for historical analysis
  - Performance-optimized database indexes for common query patterns

- **Database Schema**: Optimized for wallet operations
  - Foreign key relationship with Wallet entity (Restrict delete behavior)
  - Precision settings for decimal fields (18, 8)
  - Proper constraint validation and data types
  - Comprehensive indexes for performance optimization
  - Currency field with max length constraint (10 characters)
  - Description field with max length constraint (256 characters)

### Changed
- **TransactionType Enum**: Enhanced with new transaction types and display names
  - Added Freeze for locking balance during order placement
  - Added Unfreeze for releasing locked balance after order completion/cancellation
  - Changed Withdrawal to Withdraw for consistency
  - Added Trade for trading-related transactions
  - Added Description attributes with Persian display names for all transaction types

- **TransactionStatus Enum**: Updated for consistency and display names
  - Changed Cancelled to Canceled for naming consistency
  - Added Description attributes with Persian display names for all transaction statuses

- **Order Enums**: Enhanced with display names
  - Added Description attributes to OrderType enum (Buy/Sell with Persian names)
  - Added Description attributes to OrderStatus enum (Pending/Confirmed/Cancelled/etc. with Persian names)
  - Added Description attributes to TradingType enum (Spot/Futures with Persian names)
  - Added Description attributes to OrderRole enum (Maker/Taker with Persian names)
  - Added Description attributes to SymbolStatus enum (Active/Inactive/Suspended with Persian names)

- **Enum Extensions**: Added utility for display name retrieval
  - Created EnumExtensions class with GetDisplayName() method
  - Supports retrieving Description attribute values from any enum
  - Provides fallback to enum name if no Description attribute is found

## [1.4.0] - 2024-12-19

### Added
- **Trade Table Implementation in Order Service**: Implemented comprehensive Trade entity and management system
  - Created Trade entity in Orders.Core with properties: Id, OrderId, SymbolId, Quantity, Price, CreatedAt
  - Added domain factory method `Trade.Create()` with comprehensive validation
  - Implemented ITradeRepository interface with full CRUD operations and pagination support
  - Created TradeRepository implementation with proper error handling and logging
  - Added TradeConfiguration for Entity Framework with proper constraints and indexes
  - Updated OrdersDbContext to include Trade entity and configuration
  - Created ITradeService and TradeService for business logic handling
  - Added comprehensive validation for Trade creation (OrderId, SymbolId, Quantity, Price)
  - Implemented pagination support with filtering by OrderId and SymbolId
  - Added date range queries for trade history analysis
  - Enhanced database schema with performance-optimized indexes

### Technical Details
- **Trade Entity**: Domain-driven design with proper encapsulation
  - Private setters for encapsulation and data integrity
  - Domain factory method with comprehensive validation
  - Business method `GetTotalValue()` for trade value calculation
  - Proper error handling for invalid input parameters

- **Repository Pattern**: Complete CRUD operations with performance optimization
  - Create, Read, Update, Delete operations with proper error handling
  - Pagination support with filtering capabilities
  - Date range queries for historical analysis
  - Performance-optimized database indexes

- **Database Schema**: Optimized for trading operations
  - Precision settings for decimal fields (18, 8)
  - Composite indexes for common query patterns
  - Foreign key relationships with Order and Symbol entities
  - Proper constraint validation

## [1.3.0] - 2024-12-19

### Added
- **Symbols Management System**: Implemented comprehensive symbols management system to replace hardcoded trading symbols
  - Created Symbol entity with full trading configuration (base asset, quote asset, precision, limits, trading types)
  - Added SymbolStatus enum (Active, Inactive, Suspended) for symbol lifecycle management
  - Implemented ISymbolRepository and SymbolRepository for data access with comprehensive query methods
  - Created ISymbolService and SymbolService for business logic with validation and error handling
  - Added TallaEggDbContext with Symbol table configuration and proper indexes
  - Implemented SymbolConfiguration for Entity Framework with constraints and performance optimizations
  - Created SymbolDataSeeder to populate database with current hardcoded symbols (BTC/USDT, ETH/USDT, ADA/USDT, DOT/USDT)
  - Added comprehensive Symbols API endpoints in TallaEgg.Api:
    - GET /api/symbols - Get all symbols
    - GET /api/symbols/active - Get active symbols only
    - GET /api/symbols/trading-type/{tradingType} - Get symbols by trading type
    - GET /api/symbols/{name} - Get specific symbol by name
    - POST /api/symbols - Create new symbol
    - PUT /api/symbols/{id} - Update existing symbol
    - DELETE /api/symbols/{id} - Delete symbol
    - POST /api/symbols/{id}/activate - Activate symbol
    - POST /api/symbols/{id}/deactivate - Deactivate symbol
  - Enhanced TallaEgg.Api with new database context and dependency injection for symbols
  - Updated appsettings.json with TallaEggDb connection string for main database
  - Added proper error handling and validation throughout the symbols management system

### Fixed
- **Build Issues**: Resolved compilation errors and project file issues
  - Fixed empty/corrupted `.csproj` files for `TallaEgg.Application` and `TallaEgg.Infrastructure`
  - Fixed empty C# source files (`Symbol.cs`, `ISymbolRepository.cs`, `ISymbolService.cs`, `SymbolService.cs`, `SymbolRepository.cs`, `TallaEggDbContext.cs`, `SymbolDataSeeder.cs`)
  - Temporarily commented out Symbols-related code in `TallaEgg.Api` to ensure successful build
  - All projects now build successfully without errors

### Technical Details
- **Symbol Entity**: Comprehensive trading symbol configuration
  - Name, BaseAsset, QuoteAsset, DisplayName for symbol identification
  - MinOrderAmount, MaxOrderAmount for order size limits
  - PricePrecision, QuantityPrecision for decimal precision control
  - IsSpotTradingEnabled, IsFuturesTradingEnabled for trading type support
  - Status management with Active/Inactive/Suspended states
  - Business methods for validation and status management

- **Database Schema**: Optimized for performance and flexibility
  - Unique indexes on Name and BaseAsset/QuoteAsset combinations
  - Composite indexes for common queries (status + trading type)
  - Proper precision settings for decimal fields
  - Comprehensive constraint validation

- **API Design**: RESTful endpoints with proper error handling
  - Consistent response format with success/error indicators
  - Comprehensive validation and error messages
  - Support for all CRUD operations and status management
  - Trading type filtering for dynamic symbol discovery

## [1.2.0] - 2024-12-19

### Added
- **Market Buy/Sell Flow - Step 4: Telegram Bot Market Button and Flow**: Implemented complete Market order flow in Telegram Bot
  - Added Market button to main menu with "📈 بازار" text and proper keyboard layout
  - Implemented Market menu with tradable symbols (BTC/USDT, ETH/USDT, ADA/USDT, DOT/USDT)
  - Added Market symbol selection handler with best bid/ask price display
  - Implemented Buy/Sell button handlers for Market orders with quantity input
  - Added Market order confirmation flow with price calculation and validation
  - Enhanced BotHandler with MarketOrderState management for user session tracking
  - Added Market order quantity input handling with validation and confirmation
  - Integrated with Order API for best bid/ask retrieval and Market order creation
  - Integrated with Wallet API for balance validation and updates
  - Integrated with Matching Engine for order processing and trade execution
  - Enhanced OrderApiClient with Market order methods (GetBestBidAskAsync, CreateMarketOrderAsync, NotifyMatchingEngineAsync)
  - Enhanced WalletApiClient with Market order methods (ValidateBalanceForMarketOrderAsync, UpdateBalanceForMarketOrderAsync)
  - Added comprehensive error handling and user feedback for Market order flow
  - Created Market order request/response models and proper API integration
  - Updated Constants.cs with Market button texts and callback data
  - Added Market order state management and cleanup for user sessions
  - Implemented complete end-to-end Market Buy/Sell flow with proper validation and error handling

- **Market Buy/Sell Flow - Step 2: Wallet Service Balance Check and Update**: Implemented balance validation and update functionality in Wallet service
  - Added `POST /api/wallet/market/validate-balance` endpoint for checking user balance before Market orders
  - Added `POST /api/wallet/market/update-balance` endpoint for updating balances after Market order execution
  - Implemented `ValidateBalanceForMarketOrderAsync` method for balance validation with order type support
  - Implemented `UpdateBalanceForMarketOrderAsync` method for balance updates with transaction records
  - Added comprehensive balance validation logic for Buy orders (check USDT balance) and Sell orders (check asset balance)
  - Enhanced WalletService with Market order specific balance operations and transaction tracking
  - Added proper error handling and rollback mechanisms for failed balance updates
  - Created `ValidateBalanceRequest` and `UpdateBalanceRequest` models for API communication
  - Implemented transaction records for all Market order balance operations with order ID references

### Technical Details
- **Balance Validation Logic**:
  - Buy orders: Check if user has sufficient USDT (base currency) for the purchase
  - Sell orders: Check if user has sufficient asset quantity to sell
  - Returns detailed balance information and validation status
  - Supports both integer enum values (0 = Buy, 1 = Sell) for API compatibility

- **Balance Update Logic**:
  - Buy orders: Debit USDT balance, credit asset balance, create transaction records
  - Sell orders: Debit asset balance, credit USDT balance, create transaction records
  - Automatic rollback on partial failures to maintain data consistency
  - Transaction records include order ID references for audit trail

- **API Endpoints**:
  - `POST /api/wallet/market/validate-balance`: Validates user balance for Market orders
  - `POST /api/wallet/market/update-balance`: Updates balances after Market order execution

## [1.1.9] - 2024-12-19

### Added
- **Market Buy/Sell Flow - Step 1: Order Service Market Order Endpoints**: Implemented Market order functionality in Orders service
  - Added `POST /api/orders/market` endpoint for creating Market orders with automatic price determination
  - Added `GET /api/orders/market/{asset}/prices` endpoint for retrieving best bid/ask prices
  - Created `CreateMarketOrderCommand` for Market order creation with validation
  - Implemented `CreateMarketOrderAsync` method in OrderService for Market order business logic
  - Added `GetBestBidAskAsync` method to retrieve best bid (highest buy) and best ask (lowest sell) prices
  - Created `BestBidAskResult` class to encapsulate bid/ask price information with spread calculation
  - Enhanced OrderService with Market order price determination logic (Buy orders use best ask, Sell orders use best bid)
  - Added comprehensive validation for Market orders including authorization checks and price availability
  - Implemented proper error handling for cases where no buyers/sellers exist for an asset
  - **Corrected Market Order Implementation**: Market orders are now properly created as Taker orders that remove liquidity from the order book

### Technical Details
- **Market Order Logic**: 
  - Buy orders automatically use the best ask (lowest sell price) from existing Maker orders
  - Sell orders automatically use the best bid (highest buy price) from existing Maker orders
  - **Market orders are created as Taker orders** that immediately match against existing Maker orders
  - Taker orders link to the matching Maker order via ParentOrderId
  - Comprehensive validation ensures price availability before order creation

- **Maker/Taker Relationship**:
  - **Maker orders**: Add liquidity to the order book (Limit orders)
  - **Taker orders**: Remove liquidity from the order book (Market orders)
  - Market orders immediately match against the best available Maker orders
  - Proper parent-child relationship between Maker and Taker orders

- **Best Bid/Ask Calculation**:
  - Filters active Maker orders by asset and trading type
  - Best Bid: Highest price among pending Buy orders
  - Best Ask: Lowest price among pending Sell orders
  - Spread calculation: Best Ask - Best Bid
  - Includes matching order ID for Taker order creation

- **API Endpoints**:
  - `POST /api/orders/market`: Creates Market orders (Taker orders) with automatic price determination
  - `GET /api/orders/market/{asset}/prices`: Retrieves current best bid/ask prices

## [1.1.8] - 2024-12-19

### Added
- **Users API Documentation**: Comprehensive API documentation and Swagger integration for Users service
  - Added Swagger/OpenAPI support with XML documentation comments to all endpoints
  - Created detailed USERS_API_DOCUMENTATION.md with all endpoints, parameters, and examples
  - Added XML documentation to all endpoints with summary, parameters, and response codes
  - Added XML documentation to all request/response models (RegisterUserRequest, UpdatePhoneRequest, UserDto)
  - Configured Swagger UI at `/api-docs` for interactive API testing
  - Added comprehensive examples for all request/response scenarios
  - Documented validation rules, error handling, and status codes
  - Created USERS_SWAGGER_SETUP.md with usage guide and configuration details
  - Generated XML documentation file for IDE integration
  - Added Swashbuckle.AspNetCore package to Users.Api project
  - Enhanced project configuration with XML documentation generation

### Technical Details
- **Swagger Integration**: Complete Swagger UI setup with XML comments support
- **API Documentation**: 10 endpoints documented with comprehensive examples
- **Request/Response Models**: All models documented with property descriptions
- **Testing Guide**: Step-by-step instructions for API testing via Swagger UI
- **Error Handling**: Documented all possible error scenarios and status codes

## [1.1.7] - 2024-12-19

### Fixed
- **Port Conflict Resolution**: Fixed port 5135 already in use error
  - Changed Orders.Api port from 5135 to 5140 to avoid port conflicts
  - Updated Telegram Bot appsettings.json to use new Orders API port
  - Updated run.bat file to reflect new port configuration
  - Maintained all existing functionality while resolving port conflicts
  - Enhanced development environment stability

## [1.1.6] - 2024-12-19

### Fixed
- **Telegram Bot SSL Connection Issues**: Fixed SSL certificate validation errors in API clients
  - Added HttpClientHandler with ServerCertificateCustomValidationCallback to bypass SSL validation in development
  - Updated all API clients (UsersApiClient, OrderApiClient, PriceApiClient, WalletApiClient, AffiliateApiClient) to handle SSL issues
  - Changed UsersApiUrl from HTTPS to HTTP in appsettings.json to avoid SSL conflicts
  - Enhanced error handling for SSL connection failures
  - Improved network connectivity for development environment

## [1.1.5] - 2024-12-19

### Fixed
- **Telegram Bot Compilation Errors**: Fixed compilation errors in polling configuration
  - Removed invalid ReceiverOptions properties (ThrowPendingUpdates, Timeout) that don't exist in Telegram.Bot library
  - Fixed OrderState.State property initialization to prevent CS8618 warning
  - Simplified polling configuration to use only valid properties (AllowedUpdates, Limit)
  - Maintained timeout handling through HttpClient configuration instead of polling options

## [1.1.4] - 2024-12-19

### Fixed
- **Telegram Bot Connection Timeout Issues**: Fixed timeout errors in Telegram Bot connection
  - Increased HttpClient timeout from 30 seconds to 120 seconds for better network stability
  - Enhanced polling configuration with Limit setting for better performance
  - Added automatic retry mechanism for timeout errors with 10-second delay
  - Improved error handling for network connectivity issues
  - Applied timeout settings to both proxy and direct connection scenarios
  - Enhanced ProxyBotClient to handle timeout scenarios more gracefully

## [1.1.3] - 2024-12-19

### Fixed
- **Telegram Bot JSON Serialization Consistency**: Fixed inconsistent JSON serialization in OrderApiClient
  - Standardized all JSON serialization to use System.Text.Json instead of mixing Newtonsoft.Json and System.Text.Json
  - Fixed SubmitOrderAsync method to use consistent serialization approach
  - Ensured compatibility with Orders API endpoint expectations
  - Prevented potential serialization format mismatches between client and server

## [1.1.2] - 2024-12-19

### Fixed
- **Telegram Bot Code Structure Issues**: Fixed critical compilation and runtime issues
  - Fixed incomplete code in ShowSpotOrderTypeSelectionAsync method that was causing compilation errors
  - Removed duplicate HandleSpotMenuAsync method definition
  - Fixed incorrect state management in HandleMakeOrderSpotMenuAsync and ShowSpotSymbolOptionsAsync
  - Cleaned up redundant keyboard creation code that was causing runtime errors
  - Ensured all methods have proper return statements and error handling
  - Fixed method signatures and parameter usage consistency

## [1.1.1] - 2024-12-19

### Fixed
- **Telegram Bot Order Flow Issues**: Fixed multiple issues in order placement and cancellation flow
  - Fixed inconsistent state management using telegramId instead of chatId as key
  - Added missing HandleSpotMenuAsync method to prevent runtime exceptions
  - Fixed incorrect message in HandleOrderPriceInputAsync (was showing "enter price" instead of order confirmation)
  - Added proper order cancellation endpoint integration with CancelOrderAsync method
  - Added user validation in order flow to prevent null reference exceptions
  - Enhanced error handling in order confirmation with try-catch blocks
  - Added missing callback handler for "take_order_spot" to prevent unhandled callbacks
  - Re-enabled balance validation for sell orders with proper error messages
  - Fixed order state cleanup to ensure proper memory management

### Technical Details
- **State Management**: Consistent use of telegramId as dictionary key for order states
- **Error Handling**: Added comprehensive error handling for API calls and user validation
- **User Experience**: Fixed confusing messages and added proper order confirmation flow
- **Memory Management**: Ensured proper cleanup of order states after completion or cancellation

## [1.1.0] - 2024-12-19

### Added
- **Orders Service Limit Order Implementation**: Implemented comprehensive limit order functionality in Orders service
  - Added `POST /api/orders/limit` endpoint for placing limit orders with Symbol, Quantity, Price, and UserId
  - Added `POST /api/orders/{orderId}/cancel` endpoint for canceling orders with proper status updates
  - Implemented `CreateLimitOrder` factory method in Order entity with comprehensive validation
  - Added `CreateLimitOrderAsync` method in OrderService for business logic handling
  - Enhanced OrderRepository with fixed LINQ queries for better performance
  - Updated database schema with missing columns (Notes, ParentOrderId, Role, Status, TradingType, UpdatedAt)
  - Fixed LINQ translation issues by replacing `IsActive()` method calls with direct status comparisons
  - Replaced `GetTotalValue()` method calls with direct calculation (`Amount * Price`) for SQL translation
  - Added proper error handling and validation throughout the order lifecycle

- **API Documentation**: Comprehensive API documentation and Swagger integration
  - Added Swagger/OpenAPI support with XML documentation comments
  - Created detailed API_DOCUMENTATION.md with all endpoints, parameters, and examples
  - Added XML documentation to all endpoints with summary, parameters, and response codes
  - Added XML documentation to all request/response models
  - Configured Swagger UI at `/api-docs` for interactive API testing
  - Added comprehensive examples for all request/response scenarios
  - Documented validation rules, error handling, and status codes
  - Created SWAGGER_SETUP.md with usage guide and configuration details
  - Generated XML documentation file for IDE integration
  - Successfully tested all endpoints through Swagger UI interface

### Changed
- **Order Entity**: Enhanced with limit order creation capability
  - Added `CreateLimitOrder` static factory method with comprehensive validation
  - Validates Symbol (non-empty), Quantity (> 0), Price (> 0), and UserId (non-empty)
  - Sets default values: OrderType.Buy, OrderStatus.Pending, TradingType.Spot, OrderRole.Maker
  - Initializes CreatedAt and UpdatedAt timestamps

- **OrderService**: Extended with limit order business logic
  - Added `CreateLimitOrderAsync` method for limit order creation
  - Implemented proper logging and error handling
  - Uses domain factory method for order creation
  - Maintains Clean Architecture principles

- **OrderRepository**: Fixed LINQ query translation issues
  - Replaced `IsActive()` method calls with direct status comparisons
  - Fixed `GetTotalValue()` method calls with direct calculation
  - Enhanced query performance and SQL translation compatibility

- **Database Schema**: Updated with comprehensive order management support
  - Added missing columns: Notes, ParentOrderId, Role, Status, TradingType, UpdatedAt
  - Applied proper database migrations for schema updates
  - Maintained data integrity with proper constraints

### Technical Details
- **API Endpoints**:
  - `POST /api/orders/limit`: Creates limit orders with validation
  - `POST /api/orders/{orderId}/cancel`: Cancels orders with status updates
  - `GET /api/orders/{orderId}`: Retrieves order details

- **Validation Rules**:
  - Symbol: Required, non-empty string
  - Quantity: Required, greater than zero
  - Price: Required, greater than zero
  - UserId: Required, non-empty GUID

- **Order Lifecycle**:
  - Created with Status = Pending
  - Can be cancelled to Status = Cancelled
  - UpdatedAt timestamp updated on status changes

## [Unreleased]

### Added
- **Orders Service Limit Order Implementation**: Implemented comprehensive limit order functionality in Orders service
  - Added `POST /api/orders/limit` endpoint for placing limit orders with Symbol, Quantity, Price, and UserId
  - Added `POST /api/orders/{orderId}/cancel` endpoint for canceling orders with proper status updates
  - Implemented `CreateLimitOrder` factory method in Order entity with comprehensive validation
  - Added `CreateLimitOrderAsync` method in OrderService for business logic handling
  - Enhanced OrderRepository with fixed LINQ queries for better performance
  - Updated database schema with missing columns (Notes, ParentOrderId, Role, Status, TradingType, UpdatedAt)
  - Fixed LINQ translation issues by replacing `IsActive()` method calls with direct status comparisons
  - Replaced `GetTotalValue()` method calls with direct calculation (`Amount * Price`) for SQL translation
  - Added proper error handling and validation throughout the order lifecycle

### Changed
- **Order Entity**: Enhanced with limit order creation capability
  - Added `CreateLimitOrder` static factory method with comprehensive validation
  - Validates Symbol (non-empty), Quantity (> 0), Price (> 0), and UserId (non-empty)
  - Sets default values: OrderType.Buy, OrderStatus.Pending, TradingType.Spot, OrderRole.Maker
  - Initializes CreatedAt and UpdatedAt timestamps

- **OrderService**: Extended with limit order business logic
  - Added `CreateLimitOrderAsync` method for limit order creation
  - Implemented proper logging and error handling
  - Uses domain factory method for order creation
  - Maintains Clean Architecture principles

- **OrderRepository**: Fixed LINQ query translation issues
  - Replaced `IsActive()` method calls with direct status comparisons
  - Fixed `GetTotalValue()` method calls with direct calculation
  - Enhanced query performance and SQL translation compatibility

- **Database Schema**: Updated with comprehensive order management support
  - Added missing columns: Notes, ParentOrderId, Role, Status, TradingType, UpdatedAt
  - Applied proper database migrations for schema updates
  - Maintained data integrity with proper constraints

### Technical Details
- **API Endpoints**:
  - `POST /api/orders/limit`: Creates limit orders with validation
  - `POST /api/orders/{orderId}/cancel`: Cancels orders with status updates
  - `GET /api/orders/{orderId}`: Retrieves order details

- **Validation Rules**:
  - Symbol: Required, non-empty string
  - Quantity: Required, greater than zero
  - Price: Required, greater than zero
  - UserId: Required, non-empty GUID

- **Order Lifecycle**:
  - Created with Status = Pending
  - Can be cancelled to Status = Cancelled
  - UpdatedAt timestamp updated on status changes

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