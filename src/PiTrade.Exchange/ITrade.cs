using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange {
  public interface ITrade {
    DateTime Timestamp { get; set; }
    decimal Price { get; set; }
    decimal Quantity { get; set; }
    public long OIDBuyer { get; set; }
    public long OIDSeller { get; set; }
    decimal Commission { get; set; }
  }
}
