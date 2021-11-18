using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Entities {
  public class Order {
    public long Id { get; private set; }
    public IMarket Market { get; private set; }
    public OrderSide Side { get; private set; }
    public decimal Price { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal Amount => Price * Quantity;
    public decimal ExecutedQuantity { get; private set; } = 0;
    public decimal ExecutedAmount => Price * ExecutedQuantity;
    public IEnumerable<decimal> Fills => fills;
    public bool IsFilled => Quantity <= ExecutedQuantity;


    private IList<decimal> fills = new List<decimal>();

    public Order(long id, IMarket market, OrderSide side, decimal price, decimal quantity) {
      Id = id;
      Market = market;
      Side = side;
      Price = price;
      Quantity = quantity;
    }

    public void Fill(decimal quantity) {
      fills.Add(quantity);
      ExecutedQuantity += quantity;
    }

    public override string ToString() => $"Id = {Id}, Market = {Market}, Side = {Side}, Price = {Price}, Quantity = {Quantity}, ExecutedQuantity = {ExecutedQuantity}, Amount = {Price * Quantity}";
  }
}
