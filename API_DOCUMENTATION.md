# TallaEgg Orders API Documentation

## Overview

The TallaEgg Orders API provides comprehensive order management functionality for the trading platform.

**Base URL**: `http://localhost:5135/api`

## Endpoints

### Create Limit Order
**Method**: `POST`  
**URL**: `/orders/limit`

Creates a new limit order with specified price and quantity.

**Request Example**:
```json
{
  "symbol": "BTC",
  "quantity": 0.25,
  "price": 44000.00,
  "userId": "123e4567-e89b-12d3-a456-426614174000"
}
```

**Response Example**:
```json
{
  "success": true,
  "message": "Limit order created successfully",
  "order": { ... }
}
```

### Cancel Order
**Method**: `POST`  
**URL**: `/orders/{orderId}/cancel`

Cancels an existing order.

### Get Order by ID
**Method**: `GET`  
**URL**: `/orders/{orderId}`

Retrieves order details.

## Testing

Use Swagger UI at: `http://localhost:5135/api-docs`