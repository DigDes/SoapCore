using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace SoapCore.Tests.DuplicateFault
{
	public sealed class CountingLoggerProvider : ILoggerProvider
	{
		private readonly List<string> _errors = new List<string>();

		public IReadOnlyList<string> Errors
		{
			get
			{
				lock (_errors)
				{
					return _errors.ToList();
				}
			}
		}

		public ILogger CreateLogger(string categoryName)
		{
			return new CountingLogger(this);
		}

		public void Dispose()
		{
		}

		private void AddError(string message)
		{
			lock (_errors)
			{
				_errors.Add(message);
			}
		}

		private sealed class CountingLogger : ILogger
		{
			private readonly CountingLoggerProvider _provider;

			public CountingLogger(CountingLoggerProvider provider)
			{
				_provider = provider;
			}

			public IDisposable BeginScope<TState>(TState state)
				where TState : notnull
			{
				return null;
			}

			public bool IsEnabled(LogLevel logLevel)
			{
				return true;
			}

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
			{
				if (logLevel != LogLevel.Error)
				{
					return;
				}

				_provider.AddError(formatter(state, exception));
			}
		}
	}
}
