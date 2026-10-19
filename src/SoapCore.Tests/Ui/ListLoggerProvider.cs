using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace SoapCore.Tests.Ui
{
	// Records every log entry in memory so tests can assert on what the UI logged.
	public sealed class ListLoggerProvider : ILoggerProvider
	{
		private readonly object _gate = new object();
		private readonly List<LogEntry> _entries = new List<LogEntry>();

		public IReadOnlyList<LogEntry> Entries
		{
			get
			{
				lock (_gate)
				{
					return _entries.ToArray();
				}
			}
		}

		public ILogger CreateLogger(string categoryName)
		{
			return new ListLogger(this, categoryName);
		}

		public void Dispose()
		{
		}

		private void Add(LogLevel level, string category, string message)
		{
			lock (_gate)
			{
				_entries.Add(new LogEntry(level, category, message));
			}
		}

		public sealed class LogEntry
		{
			public LogEntry(LogLevel level, string category, string message)
			{
				Level = level;
				Category = category;
				Message = message;
			}

			public LogLevel Level { get; }

			public string Category { get; }

			public string Message { get; }
		}

		private sealed class ListLogger : ILogger
		{
			private readonly ListLoggerProvider _owner;
			private readonly string _category;

			public ListLogger(ListLoggerProvider owner, string category)
			{
				_owner = owner;
				_category = category;
			}

			public IDisposable BeginScope<TState>(TState state)
			{
				return null;
			}

			public bool IsEnabled(LogLevel logLevel)
			{
				return true;
			}

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
			{
				_owner.Add(logLevel, _category, formatter(state, exception));
			}
		}
	}
}
