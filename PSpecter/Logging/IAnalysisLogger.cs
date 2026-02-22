using System;

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
            Console.Error.WriteLine($"DEBUG: {message}");
        }

        public void Verbose(string message)
        {
            Console.Error.WriteLine($"VERBOSE: {message}");
        }

        public void Warning(string message)
        {
            Console.Error.WriteLine($"WARNING: {message}");
        }

        public void Error(string message)
        {
            Console.Error.WriteLine($"ERROR: {message}");
        }

        public void Error(string message, Exception exception)
        {
            Console.Error.WriteLine($"ERROR: {message}: {exception}");
        }
    }
}
