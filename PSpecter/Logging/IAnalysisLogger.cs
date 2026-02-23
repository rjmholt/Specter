using System;
using System.IO;

namespace PSpecter.Logging
{
    public interface IAnalysisLogger
    {
        void Debug(string message);

        void Verbose(string message);

        void Warning(string message);

        void Error(string message);

        void Error(string message, Exception exception);
    }

    public sealed class NullAnalysisLogger : IAnalysisLogger
    {
        public static readonly NullAnalysisLogger Instance = new();

        public void Debug(string message) { }

        public void Verbose(string message) { }

        public void Warning(string message) { }

        public void Error(string message) { }

        public void Error(string message, Exception exception) { }
    }

    public sealed class ConsoleAnalysisLogger : IAnalysisLogger
    {
        public static readonly ConsoleAnalysisLogger Instance = new();

        public void Debug(string message)
        {
            WriteColored(ConsoleColor.DarkGray, "DEBUG", message);
        }

        public void Verbose(string message)
        {
            WriteColored(ConsoleColor.Cyan, "VERBOSE", message);
        }

        public void Warning(string message)
        {
            WriteColored(ConsoleColor.Yellow, "WARNING", message);
        }

        public void Error(string message)
        {
            WriteColored(ConsoleColor.Red, "ERROR", message);
        }

        public void Error(string message, Exception exception)
        {
            WriteColored(ConsoleColor.Red, "ERROR", $"{message}: {exception}");
        }

        private static void WriteColored(ConsoleColor color, string prefix, string message)
        {
            try
            {
                ConsoleColor previous = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.Error.Write(prefix);
                Console.ForegroundColor = previous;
                Console.Error.Write(": ");
                Console.Error.WriteLine(message);
            }
            catch
            {
                Console.Error.WriteLine($"{prefix}: {message}");
            }
        }
    }

    /// <summary>
    /// Append-only file logger for daemon/server mode.
    /// Thread-safe via lock; writes are flushed immediately.
    /// </summary>
    public sealed class FileAnalysisLogger : IAnalysisLogger, IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly object _lock = new();

        public FileAnalysisLogger(string filePath)
        {
            _writer = new StreamWriter(filePath, append: true) { AutoFlush = true };
        }

        public void Debug(string message) => Write("DEBUG", message);

        public void Verbose(string message) => Write("VERBOSE", message);

        public void Warning(string message) => Write("WARNING", message);

        public void Error(string message) => Write("ERROR", message);

        public void Error(string message, Exception exception) => Write("ERROR", $"{message}: {exception}");

        public void Dispose()
        {
            lock (_lock)
            {
                _writer.Dispose();
            }
        }

        private void Write(string level, string message)
        {
            lock (_lock)
            {
                _writer.Write(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                _writer.Write(" [");
                _writer.Write(level);
                _writer.Write("] ");
                _writer.WriteLine(message);
            }
        }
    }

    /// <summary>
    /// Multiplexes log messages to multiple loggers.
    /// </summary>
    public sealed class CompositeAnalysisLogger : IAnalysisLogger
    {
        private readonly IAnalysisLogger[] _loggers;

        public CompositeAnalysisLogger(params IAnalysisLogger[] loggers)
        {
            _loggers = loggers;
        }

        public void Debug(string message)
        {
            for (int i = 0; i < _loggers.Length; i++)
            {
                _loggers[i].Debug(message);
            }
        }

        public void Verbose(string message)
        {
            for (int i = 0; i < _loggers.Length; i++)
            {
                _loggers[i].Verbose(message);
            }
        }

        public void Warning(string message)
        {
            for (int i = 0; i < _loggers.Length; i++)
            {
                _loggers[i].Warning(message);
            }
        }

        public void Error(string message)
        {
            for (int i = 0; i < _loggers.Length; i++)
            {
                _loggers[i].Error(message);
            }
        }

        public void Error(string message, Exception exception)
        {
            for (int i = 0; i < _loggers.Length; i++)
            {
                _loggers[i].Error(message, exception);
            }
        }
    }
}
