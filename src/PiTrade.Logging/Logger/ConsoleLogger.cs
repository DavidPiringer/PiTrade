using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Logging.Logger {
  public class ConsoleLogger : ILogger {
    private readonly object locker = new object();
    private readonly ConsoleColor defaultColor = ConsoleColor.White;

    public void Error(string message) {
      lock (locker) {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(FormatMessage("ERROR", message));
        Console.ForegroundColor = defaultColor;
      }
    }

    public void Info(string message) {
      lock (locker) {
        Console.WriteLine(FormatMessage("INFO", message));
      }
    }

    public void Warn(string message) {
      lock (locker) {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(FormatMessage("WARN", message));
        Console.ForegroundColor = defaultColor;
      }
    }

    private static string FormatMessage(string prefix, string message) => $"[{prefix}]@[{DateTime.Now.ToString("{dd.MM.yy - H:mm:ss}")}] {message}";
  }
}
