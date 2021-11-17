using PiTrade.Logging.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Logging {
  public static class Log {
    public static ILogger Logger { get; set; } = new ConsoleLogger();

    public static void Info(object obj) => Info(obj?.ToString() ?? "");
    public static void Info(string message) => Logger.Info(message);

    public static void Warn(object obj) => Warn(obj?.ToString() ?? "");
    public static void Warn(string message) => Logger.Warn(message);

    public static void Error(object obj) => Error(obj?.ToString() ?? "");
    public static void Error(string message) => Logger.Error(message);
  }
}
