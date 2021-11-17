using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Strategy.Domain
{
  internal struct BuyStep
  {
    public decimal Price { get; }
    public decimal Quantity { get; }
    public decimal Amount => Price * Quantity;

    public BuyStep(decimal price, decimal quantity)
    {
      Price = price;
      Quantity = quantity;
    }

    public override string ToString() =>
      $"[Price = {Price}, Quantity = {Quantity}, Amount = {Price * Quantity}]";
  }
}
