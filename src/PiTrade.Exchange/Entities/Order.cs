using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange.Entities {
  public class Order {
    private readonly object locker = new object();

    public long Id { get; private set; }
    public IMarket Market { get; private set; }
    public OrderSide Side { get; private set; }
    public decimal Price { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal Amount => Price * Quantity;
    public decimal ExecutedQuantity { get; private set; }
    public decimal ExecutedAmount {
      get {
        lock (locker) return Price * ExecutedQuantity;
      }
    }

    public IEnumerable<OrderFill> Fills => fills.ToArray();

    public bool IsFilled {
      get {
        lock (locker) return Quantity <= ExecutedQuantity;
      }
    }

    public decimal AvgFillPrice => Fills.Average(x => x.Price);


    private ConcurrentBag<OrderFill> fills = new ConcurrentBag<OrderFill>();

    public Order(long id, IMarket market, OrderSide side, decimal price, decimal quantity) {
      Id = id;
      Market = market;
      Side = side;
      Price = price;
      Quantity = quantity;
    }

    internal void Fill(decimal quantity, decimal price) {
      fills.Add(new OrderFill(quantity, price));
      lock (locker) {
        ExecutedQuantity += quantity;
      }
    }

    public override string ToString() => 
      $"Id = {Id}, " +
      $"Market = {Market}, " +
      $"Side = {Side}, " +
      $"Price = {Price}, " +
      $"Quantity = {Quantity}, " +
      $"ExecutedQuantity = {ExecutedQuantity}, " +
      $"Amount = {Price * Quantity}";

    // TODO: dispose itself -> if not filled -> CancelOrder
  }
}
