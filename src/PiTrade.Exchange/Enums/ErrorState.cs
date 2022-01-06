using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Enums {
  public enum ErrorState {
    ConnectionLost,
    MinNominalUndershot,
    IdNotFound,
    Undefined,
    None
  }
}
