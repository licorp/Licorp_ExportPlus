using System;
using System.IO;
using Serilog;

namespace LicorpExportPlus
{
    public static class Logger
    {
        private const string AppName = "ExportPlus";
        private static readonly string LogFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Licorp",
            AppName,
            "Logs"
        );

        private static readonly ILogger _logger;

        static Logger()
        {
            if (!Directory.Exists(LogFolder))
            {
                Directory.CreateDirectory(LogFolder);
            }

            string logFile = Path.Combine(LogFolder, "exportplus_.log");

            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithProperty("Application", AppName)
                .WriteTo.File(logFile, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
                .CreateLogger();
        }

        public static void Debug(string message) => _logger.Debug(message);
        public static void Debug(string message, params object[] args) => _logger.Debug(message, args);

        public static void Information(string message) => _logger.Information(message);
        public static void Information(string message, params object[] args) => _logger.Information(message, args);
        public static void Info(string message) => _logger.Information(message);
        public static void Info(string message, params object[] args) => _logger.Information(message, args);

        public static void Warning(string message) => _logger.Warning(message);
        public static void Warning(string message, params object[] args) => _logger.Warning(message, args);

        public static void Error(string message) => _logger.Error(message);
        public static void Error(string message, params object[] args) => _logger.Error(message, args);
        public static void Error(System.Exception ex, string message) => _logger.Error(ex, message);
        public static void Error(System.Exception ex, string message, params object[] args) => _logger.Error(ex, message, args);

        public static void CloseAndFlush() => Log.CloseAndFlush();
    }
}
