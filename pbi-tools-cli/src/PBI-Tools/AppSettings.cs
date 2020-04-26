// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Serilog.Core;
using Serilog.Events;

namespace PbiTools
{
    public class AppSettings
    {
        public static string EnvPrefix => "PBITOOLS_";

        public AppSettings()
        {
            // The Console log level can optionally be configured vai an environment variable:
            var envLogLevel = Environment.GetEnvironmentVariable($"{EnvPrefix}LogLevel");
            var initialLogLevel = envLogLevel != null && Enum.TryParse<LogEventLevel>(envLogLevel, out var logLevel)
                ? logLevel
                : LogEventLevel.Information; // Default log level
            this.LevelSwitch = new LoggingLevelSwitch(
#if DEBUG
                // A DEBUG compilation will always log at the Verbose level
                LogEventLevel.Verbose
#else
                initialLogLevel
#endif
            );
        }

        public LoggingLevelSwitch LevelSwitch { get; }

        internal bool ShouldSuppressConsoleLogs { get; set; } = false;

        /// <summary>
        /// Completely disables the console logger inside an <c>IDisposable</c> scope.
        /// </summary>
        public IDisposable SuppressConsoleLogs()
        {
            var prevValue = this.ShouldSuppressConsoleLogs;
            this.ShouldSuppressConsoleLogs = true;
            return new Disposable(()=>
            {
                this.ShouldSuppressConsoleLogs = prevValue;
            });
        }

        /// <summary>
        /// Resets the console log level to the specified level inside an <c>IDisposable</c> scope.
        /// </summary>
        public IDisposable SetScopedLogLevel(LogEventLevel logLevel)
        {
            var prevLogLevel = this.LevelSwitch.MinimumLevel;
            this.LevelSwitch.MinimumLevel = logLevel;
            return new Disposable(() => 
            {
                this.LevelSwitch.MinimumLevel = prevLogLevel;
            });
        }

        private class Disposable : IDisposable
        {
            private readonly Action _disposeAction;

            public Disposable(Action disposeAction)
            {
                this._disposeAction = disposeAction ?? throw new ArgumentNullException(nameof(disposeAction));
            }
            void IDisposable.Dispose()
            {
                _disposeAction();
            }
        }
    }

}
