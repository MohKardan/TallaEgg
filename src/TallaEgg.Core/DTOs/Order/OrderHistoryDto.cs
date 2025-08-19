using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TallaEgg.Core.Enums.Order;

namespace TallaEgg.Core.DTOs.Order
{
    public class OrderHistoryDto
    {
        public Guid Id { get; set; }
        public string Asset { get; set; }
        public decimal Amount { get; set; }
        public decimal Price { get; set; }
        public OrderType Type { get; set; }
        public OrderStatus Status { get; set; }
        public TradingType TradingType { get; set; }
        public OrderRole Role { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? Notes { get; set; }
    }
    public class OrderDto
    {
        public Guid Id { get; private set; }
        public string Asset { get; private set; }
        public decimal Amount { get; private set; }
        public decimal Price { get; private set; }
        public Guid UserId { get; private set; }
        public OrderType Type { get; private set; }
        public OrderStatus Status { get; private set; }
        public TradingType TradingType { get; private set; }
        public OrderRole Role { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public string? Notes { get; private set; }
        public Guid? ParentOrderId { get; private set; } // برای Taker orders که به Maker order متصل می‌شوند
    }
    }
