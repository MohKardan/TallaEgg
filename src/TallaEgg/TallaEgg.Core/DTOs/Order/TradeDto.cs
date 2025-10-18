using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TallaEgg.Core.DTOs.Order
{

    /// <summary>
    /// DTO معامله برای پاسخ
    /// </summary>
    public class TradeDto
    {
        public Guid Id { get; set; }
        public Guid BuyOrderId { get; set; }
        public Guid SellOrderId { get; set; }
        public Guid MakerOrderId { get; set; }
        public Guid TakerOrderId { get; set; }
        public string Symbol { get; set; }
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public decimal QuoteQuantity { get; set; }
        public Guid BuyerUserId { get; set; }
        public Guid SellerUserId { get; set; }
        public Guid MakerUserId { get; set; }
        public Guid TakerUserId { get; set; }
        public decimal FeeBuyer { get; set; }
        public decimal FeeSeller { get; set; }
        public decimal MakerFee { get; set; }
        public decimal TakerFee { get; set; }
        public decimal MakerFeeRate { get; set; }
        public decimal TakerFeeRate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
