using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Networking {
  public static class ExponentialBackoff {
    public static async Task Try(Func<Task<bool>> comparison, int startDelay = 500) {
      int delay = startDelay;
      while (await comparison()) {
        await Task.Delay(delay);
        delay *= 2;
      }
    }
  }
}
