# Users API Swagger Setup Guide

## Overview

This guide explains how to set up and use Swagger UI for the TallaEgg Users API documentation and testing.

## Accessing Swagger UI

Once the Users API is running, you can access Swagger UI at:

**URL**: `http://localhost:5136/api-docs`

## Features

### 1. Interactive API Documentation
- Complete endpoint documentation with descriptions
- Request/response examples
- Parameter descriptions and validation rules
- Status code explanations

### 2. API Testing
- Execute API calls directly from the browser
- View real-time responses
- Test different request parameters
- Validate API behavior

### 3. Request/Response Examples
- Pre-filled request examples
- Response format documentation
- Error response examples

## How to Use Swagger UI

### 1. Navigate to Swagger UI
1. Start the Users API service
2. Open your browser
3. Navigate to `http://localhost:5136/api-docs`

### 2. Explore Endpoints
- All API endpoints are listed with their HTTP methods
- Click on any endpoint to expand its details
- View the endpoint description, parameters, and response formats

### 3. Test an Endpoint
1. Click on the endpoint you want to test
2. Click the "Try it out" button
3. Fill in the required parameters
4. Click "Execute"
5. View the response

### 4. Example: Register User
1. Find the `POST /api/user/register` endpoint
2. Click "Try it out"
3. Enter the request body:
```json
{
  "telegramId": 123456789,
  "username": "test_user",
  "firstName": "Test",
  "lastName": "User",
  "invitationCode": "ADMIN2024"
}
```
4. Click "Execute"
5. View the response

## Configuration

### Swagger Configuration in Program.cs

```csharp
// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "TallaEgg Users API", Version = "v1" });
    
    // Include XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Add Swagger middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TallaEgg Users API v1");
    c.RoutePrefix = "api-docs";
});
```

### XML Documentation Setup

The project is configured to generate XML documentation:

```xml
<PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

## Endpoint Documentation Examples

### XML Comments for Endpoints

```csharp
/// <summary>
/// Registers a new user in the system
/// </summary>
/// <param name="request">User registration request containing Telegram ID, invitation code, and user details</param>
/// <param name="userService">User service for business logic</param>
/// <returns>Registered user details with success status</returns>
/// <response code="200">User registered successfully</response>
/// <response code="400">Invalid request data or validation error</response>
app.MapPost("/api/user/register", async (RegisterUserRequest request, UserService userService) =>
{
    // Implementation
});
```

### XML Comments for Models

```csharp
/// <summary>
/// Request model for user registration
/// </summary>
public class RegisterUserRequest
{
    /// <summary>
    /// Telegram ID of the user (required)
    /// </summary>
    public long TelegramId { get; set; }
    
    /// <summary>
    /// Telegram username (optional)
    /// </summary>
    public string? Username { get; set; }
}
```

## Testing Scenarios

### 1. User Registration Flow
1. Test user registration with valid invitation code
2. Test user registration with invalid invitation code
3. Test duplicate Telegram ID registration

### 2. User Management Flow
1. Update user phone number
2. Update user status
3. Update user role
4. Get user by Telegram ID

### 3. Invitation Code Flow
1. Validate invitation code
2. Get user ID by invitation code
3. Register user with invitation

### 4. User Queries
1. Get users by role
2. Check user existence
3. List all users

## Troubleshooting

### Common Issues

1. **Swagger UI not loading**
   - Ensure the API is running on the correct port
   - Check if Swagger package is installed
   - Verify XML documentation file is generated

2. **XML comments not showing**
   - Ensure `GenerateDocumentationFile` is set to `true`
   - Check if XML file exists in the output directory
   - Verify XML file path in Swagger configuration

3. **Endpoints not documented**
   - Add XML comments to all endpoints
   - Ensure proper parameter documentation
   - Check for compilation errors

### Debug Steps

1. Check if the API is running:
   ```bash
   curl http://localhost:5136/api-docs
   ```

2. Verify XML file generation:
   - Look for `Users.Api.xml` in the output directory
   - Check if the file contains documentation

3. Check Swagger configuration:
   - Verify Swagger services are registered
   - Ensure middleware is configured correctly

## Security Considerations

### Development vs Production

- **Development**: Swagger UI is enabled for easy testing
- **Production**: Consider disabling Swagger UI or restricting access

### Access Control

In production, you may want to:
- Disable Swagger UI entirely
- Add authentication to Swagger UI
- Restrict access to specific IP addresses
- Use environment-specific configuration

## Best Practices

1. **Documentation**
   - Add comprehensive XML comments to all endpoints
   - Include parameter descriptions and validation rules
   - Document all possible response codes

2. **Testing**
   - Use Swagger UI for initial API testing
   - Create automated tests for critical endpoints
   - Test error scenarios and edge cases

3. **Maintenance**
   - Keep documentation up to date with code changes
   - Review and update examples regularly
   - Monitor API usage and performance

## Additional Resources

- [Swagger UI Documentation](https://swagger.io/tools/swagger-ui/)
- [ASP.NET Core Swagger Integration](https://docs.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-swashbuckle)
- [XML Documentation Comments](https://docs.microsoft.com/en-us/dotnet/csharp/codedoc)
