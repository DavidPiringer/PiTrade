using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Networking {
  public static class ExponentialBackoff {
    public static async Task Try(Func<Task<bool>> comparison, int startDelay = 500, int tries = 10) {
      int delay = startDelay;
      int count = 0;
      while (await comparison() && count < tries) {
        await Task.Delay(delay);
        delay *= 2;
        count++;
      }
    }
  }
}
