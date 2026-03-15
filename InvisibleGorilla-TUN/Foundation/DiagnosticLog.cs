using System;
using System.IO;
using System.Threading;

namespace InvisibleGorillaTUN.Foundation
{
    internal static class DiagnosticLog
    {
        private static readonly string LogFilePath = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "tun-diagnostic.log");

        private static readonly object LockObj = new object();

        public static void Clear()
        {
            try
            {
                lock (LockObj)
                {
                    File.WriteAllText(
                        LogFilePath,
                        $"=== TUN Diagnostic Log Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}");
                }
            }
            catch
            {
                // Logging must never crash the service.
            }
        }

        public static void Write(string tag, string message)
        {
            try
            {
                string line =
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] " +
                    $"[T{Thread.CurrentThread.ManagedThreadId}] " +
                    $"[{tag}] {message}";

                lock (LockObj)
                {
                    File.AppendAllText(LogFilePath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Logging must never crash the service.
            }
        }

        public static void WriteException(string tag, Exception ex)
        {
            Write(tag, $"EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            Write(tag, $"StackTrace: {ex.StackTrace}");
        }
    }
}
