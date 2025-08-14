# Users API Documentation

## Overview

The Users API provides comprehensive user management functionality for the TallaEgg exchange platform. This API handles user registration, profile management, invitation codes, role management, and user status updates.

**Base URL**: `http://localhost:5136/api`

**Swagger UI**: `http://localhost:5136/api-docs`

## Authentication

Currently, the API does not require authentication for development purposes. In production, proper authentication and authorization should be implemented.

## Response Format

All API responses follow a consistent format:

```json
{
  "success": true,
  "message": "Operation completed successfully",
  "data": { ... }
}
```

## Endpoints

### 1. Register User

**Description**: Registers a new user in the system with invitation code validation.

**Method**: `POST`

**URL**: `/api/user/register`

**Request Body**:
```json
{
  "telegramId": 123456789,
  "username": "john_doe",
  "firstName": "John",
  "lastName": "Doe",
  "invitationCode": "ADMIN2024"
}
```

**Parameters**:
- `telegramId` (long, required): Telegram ID of the user
- `username` (string, optional): Telegram username
- `firstName` (string, optional): User's first name
- `lastName` (string, optional): User's last name
- `invitationCode` (string, required): Valid invitation code

**Response**:
```json
{
  "success": true,
  "message": "User loaded successfully",
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "telegramId": 123456789,
    "phoneNumber": null,
    "username": "john_doe",
    "firstName": "John",
    "lastName": "Doe",
    "createdAt": "2024-12-19T10:30:00Z",
    "lastActiveAt": null,
    "isActive": true,
    "status": "Pending"
  }
}
```

**Status Codes**:
- `200`: User registered successfully
- `400`: Invalid request data or validation error

---

### 2. Update Phone Number

**Description**: Updates the phone number for an existing user.

**Method**: `POST`

**URL**: `/api/user/update-phone`

**Request Body**:
```json
{
  "telegramId": 123456789,
  "phoneNumber": "+989123456789"
}
```

**Parameters**:
- `telegramId` (long, required): Telegram ID of the user
- `phoneNumber` (string, required): New phone number

**Response**:
```json
{
  "success": true,
  "message": "Phone number updated successfully",
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "telegramId": 123456789,
    "phoneNumber": "+989123456789",
    "username": "john_doe",
    "firstName": "John",
    "lastName": "Doe",
    "createdAt": "2024-12-19T10:30:00Z",
    "lastActiveAt": "2024-12-19T11:00:00Z",
    "isActive": true,
    "status": "Active"
  }
}
```

**Status Codes**:
- `200`: Phone number updated successfully
- `400`: Invalid request data or validation error
- `404`: User not found

---

### 3. Get User by Telegram ID

**Description**: Retrieves user information by Telegram ID.

**Method**: `GET`

**URL**: `/api/user/{telegramId}`

**Parameters**:
- `telegramId` (long, required): Telegram ID of the user

**Response**:
```json
{
  "success": true,
  "message": "User loaded successfully",
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "telegramId": 123456789,
    "phoneNumber": "+989123456789",
    "username": "john_doe",
    "firstName": "John",
    "lastName": "Doe",
    "createdAt": "2024-12-19T10:30:00Z",
    "lastActiveAt": "2024-12-19T11:00:00Z",
    "isActive": true,
    "status": "Active"
  }
}
```

**Status Codes**:
- `200`: User found and returned successfully
- `404`: User not found

---

### 4. Update User Status

**Description**: Updates the status of an existing user.

**Method**: `POST`

**URL**: `/api/user/update-status`

**Request Body**:
```json
{
  "telegramId": 123456789,
  "status": "Active"
}
```

**Parameters**:
- `telegramId` (long, required): Telegram ID of the user
- `status` (UserStatus, required): New status (Pending, Active, Suspended, Blocked)

**Response**:
```json
{
  "success": true,
  "message": "وضعیت کاربر با موفقیت به‌روزرسانی شد."
}
```

**Status Codes**:
- `200`: User status updated successfully
- `400`: Invalid request data or validation error
- `404`: User not found

---

### 5. Get User ID by Invitation Code

**Description**: Gets user ID associated with an invitation code.

**Method**: `GET`

**URL**: `/api/user/getUserIdByInvitationCode/{invitationCode}`

**Parameters**:
- `invitationCode` (string, required): Invitation code to lookup

**Response**:
```json
"550e8400-e29b-41d4-a716-446655440000"
```

**Status Codes**:
- `200`: User ID found and returned
- `400`: Invalid invitation code or error occurred
- `404`: Invitation code not found

---

### 6. Validate Invitation Code

**Description**: Validates an invitation code.

**Method**: `POST`

**URL**: `/api/user/validate-invitation`

**Request Body**:
```json
{
  "invitationCode": "ADMIN2024"
}
```

**Parameters**:
- `invitationCode` (string, required): Invitation code to validate

**Response**:
```json
{
  "isValid": true,
  "message": "Invitation code is valid"
}
```

**Status Codes**:
- `200`: Invitation code validated successfully
- `400`: Invalid invitation code or error occurred

---

### 7. Register User with Invitation

**Description**: Registers a new user with invitation code.

**Method**: `POST`

**URL**: `/api/user/register-with-invitation`

**Request Body**:
```json
{
  "user": {
    "telegramId": 123456789,
    "username": "john_doe",
    "firstName": "John",
    "lastName": "Doe",
    "invitationCode": "ADMIN2024"
  }
}
```

**Parameters**:
- `user` (User, required): Complete user object with invitation code

**Response**:
```json
{
  "success": true,
  "userId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Status Codes**:
- `200`: User registered successfully with invitation
- `400`: Invalid request data or validation error

---

### 8. Update User Role

**Description**: Updates the role of an existing user.

**Method**: `POST`

**URL**: `/api/user/update-role`

**Request Body**:
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "newRole": "Admin"
}
```

**Parameters**:
- `userId` (Guid, required): User ID to update
- `newRole` (UserRole, required): New role (RegularUser, Accountant, Admin, SuperAdmin, User)

**Response**:
```json
{
  "success": true,
  "message": "نقش کاربر با موفقیت به‌روزرسانی شد."
}
```

**Status Codes**:
- `200`: User role updated successfully
- `400`: Invalid request data or validation error
- `404`: User not found

---

### 9. Get Users by Role

**Description**: Gets all users with a specific role.

**Method**: `GET`

**URL**: `/api/users/by-role/{role}`

**Parameters**:
- `role` (string, required): Role to filter users by

**Response**:
```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "telegramId": 123456789,
    "phoneNumber": "+989123456789",
    "username": "john_doe",
    "firstName": "John",
    "lastName": "Doe",
    "createdAt": "2024-12-19T10:30:00Z",
    "lastActiveAt": "2024-12-19T11:00:00Z",
    "isActive": true,
    "status": "Active"
  }
]
```

**Status Codes**:
- `200`: Users found and returned successfully
- `400`: Invalid role or error occurred

---

### 10. Check User Existence

**Description**: Checks if a user exists by Telegram ID.

**Method**: `GET`

**URL**: `/api/user/exists/{telegramId}`

**Parameters**:
- `telegramId` (long, required): Telegram ID to check

**Response**:
```json
{
  "exists": true
}
```

**Status Codes**:
- `200`: User existence check completed
- `400`: Error occurred during check

---

## Data Models

### UserDto

```json
{
  "id": "Guid",
  "telegramId": "long",
  "phoneNumber": "string?",
  "username": "string?",
  "firstName": "string?",
  "lastName": "string?",
  "createdAt": "DateTime",
  "lastActiveAt": "DateTime?",
  "isActive": "bool",
  "status": "UserStatus"
}
```

### UserStatus Enum

- `Pending`: منتظر تایید (Waiting for approval)
- `Active`: فعال (Active)
- `Suspended`: معلق (Suspended)
- `Blocked`: مسدود (Blocked)

### UserRole Enum

- `RegularUser`: کاربر معمولی (Regular user)
- `Accountant`: حسابدار (Accountant)
- `Admin`: مدیر (Admin)
- `SuperAdmin`: مدیر ارشد (Super admin)
- `User`: کاربر (User)

## Error Handling

The API returns consistent error responses:

```json
{
  "success": false,
  "message": "Error description"
}
```

Common error scenarios:
- Invalid invitation codes
- User not found
- Duplicate Telegram IDs
- Invalid role or status values
- Database connection errors

## Testing

You can test the API using:

1. **Swagger UI**: Visit `http://localhost:5136/api-docs`
2. **Postman**: Import the endpoints using the examples above
3. **cURL**: Use the provided request/response examples

## Rate Limiting

Currently, no rate limiting is implemented. In production, consider implementing rate limiting to prevent abuse.

## CORS

The API is configured to allow CORS from any origin for development. In production, configure CORS to allow only specific origins.

## Database

The API uses SQL Server with Entity Framework Core. Ensure the database is properly configured and migrations are applied before using the API.
