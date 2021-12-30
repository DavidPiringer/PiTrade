using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange {
  public class Order : IDisposable {
    private readonly object locker = new object();

    public long Id { get; }
    public IMarket Market { get; }
    public OrderSide Side { get; }
    public decimal Price { get; }
    public decimal Quantity { get; }
    public decimal Amount => Price * Quantity;
    public decimal ExecutedQuantity => Fills.Sum(x => x.Quantity);
    public decimal ExecutedAmount => Price * ExecutedQuantity;
    public IEnumerable<OrderFill> Fills => fills.ToArray();
    public bool IsFilled => Quantity <= ExecutedQuantity;
    public decimal AvgFillPrice => Fills.Average(x => x.Price);


    private ConcurrentBag<OrderFill> fills = new ConcurrentBag<OrderFill>();
    private bool disposedValue;

    public Order(long id, IMarket market, OrderSide side, decimal price, decimal quantity) {
      Id = id;
      Market = market;
      Side = side;
      Price = price;
      Quantity = quantity;
    }

    internal void Fill(decimal quantity, decimal price) {
      fills.Add(new OrderFill(quantity, price));
    }

    // TODO: NewOrder -> Returns Task<Order?> (no exceptions)
    // TODO: Filled Status await with FillTask -> if and set mutex in fill

    public override string ToString() => 
      $"Id = {Id}, " +
      $"Market = {Market}, " +
      $"Side = {Side}, " +
      $"Price = {Price}, " +
      $"Quantity = {Quantity}, " +
      $"ExecutedQuantity = {ExecutedQuantity}, " +
      $"Amount = {Price * Quantity}";


    protected virtual void Dispose(bool disposing) {
      if (!disposedValue) {
        if (disposing) {
          // TODO: dispose managed state (managed objects)
        }

        // TODO: free unmanaged resources (unmanaged objects) and override finalizer
        // TODO: set large fields to null
        disposedValue = true;
      }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    ~Order() {
      // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
      Dispose(disposing: false);
    }

    public void Dispose() {
      // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }

    // TODO: dispose itself -> if not filled -> CancelOrder
  }
}
