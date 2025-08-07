# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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