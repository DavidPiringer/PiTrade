using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Domain
{
  public class Order
  {
    public int Id { get; private set; }
    public Market Market { get; private set; }
    public OrderType Type { get; private set; }
    public decimal Price { get; private set; }
    public decimal Quantity { get; private set; }

    public Order(int id, Market market, OrderType type, decimal price, decimal quantity)
    {
      Id = id;
      Market = market;
      Type = type;
      Price = price;
      Quantity = quantity;
    }

    public override string ToString() => $"Id = {Id}, Market = {Market}, Type = {Type}";
  }
}
