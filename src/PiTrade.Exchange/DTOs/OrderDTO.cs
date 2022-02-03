using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange.DTOs {
  public struct OrderDTO {
    public long Id { get; set; }
    public IMarket Market { get; set; }
    public OrderSide Side { get; set; }
    public decimal TargetPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal ExecutedQuantity { get; set; }
    public decimal AvgFillPrice { get; set; }
    public OrderState State { get; set; }
  }
}
