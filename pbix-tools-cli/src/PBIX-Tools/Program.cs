﻿using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PowerArgs;
using Serilog;
using Serilog.Core;
using Serilog.Events;

[assembly: InternalsVisibleTo("pbix-tools.tests")]

namespace PbixTools
{

    class Program
    {
        [DllImport("kernel32.dll")]
        private static extern ErrorModes SetErrorMode(ErrorModes uMode);

        [Flags]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum ErrorModes : uint
        {
            SYSTEM_DEFAULT = 0x0,
            SEM_FAILCRITICALERRORS = 0x0001,
            SEM_NOALIGNMENTFAULTEXCEPT = 0x0004,
            SEM_NOGPFAULTERRORBOX = 0x0002,
            SEM_NOOPENFILEERRORBOX = 0x8000
        }

        static Program()
        {
            // Prevent the "This program has stopped working" messages.
            var prevMode = SetErrorMode(ErrorModes.SEM_NOGPFAULTERRORBOX);
            SetErrorMode(prevMode | ErrorModes.SEM_NOGPFAULTERRORBOX); // Set error mode w/o overwriting prev settings

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(AppSettings.LevelSwitch)
                .WriteTo.Console()
                    .Filter.ByExcluding(e => AppSettings.ShouldSuppressConsoleLogs)
                .CreateLogger();
        }

        //internal static IConfigurationRoot Configuration { get; }
        internal static AppSettings AppSettings { get; } = new AppSettings();

        static int Main(string[] args)
        {
            var argsDef = CmdLineArgumentsDefinitionExtensions.For<CmdLineActions>().RemoveAutoAliases();

            // When invoked w/o args, print usage and exit immediately (do not trigger ArgException)
            if ((args ?? new string[0]).Length == 0)
            {
                ArgUsage.GenerateUsageFromTemplate(argsDef).WriteLine();
                return (int)ExitCode.NoArgsProvided;
            }

            var result = default(ExitCode);
            try
            {
                var action = Args.ParseAction(argsDef, args);
                
                action.Invoke(); // throws ArgException

                // in Debug compilation, propagates any exceptions thrown by executing action
                // in Release compilation, a user friendly error message is displayed, and exceptions thrown are available via the HandledException property

                /* This branch only applies in Release mode, and only to parser exceptions */
                if (action.HandledException != null)
                {
                    // Standard output has been generated by PowerArgs framework already
                    Console.WriteLine();
                    Log.Logger.Verbose(action.HandledException, "PowerArgs exception");
                }

                // TODO Define and handle specific exceptions to report back to user directly (No PBI install, etc...)

                result = action.HandledException == null ? ExitCode.Success : ExitCode.InvalidArgs;
            }
            catch (ArgException ex) // this will only be hit in DEBUG complation, hence should only matter to devs
            {
                if (!Environment.UserInteractive)
                {
                    Log.Logger.Fatal(ex, "Bad user input.");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine(ex.Message);
                    Console.ResetColor();

                    ArgUsage.GenerateUsageFromTemplate<CmdLineActions>().WriteLine();
                }
                result = ExitCode.InvalidArgs;
            }
            catch (Exception ex) /* Any other unhandled exception (debug and release) */
            {
                // TODO Explicitly log into crash file...
                // If CWD is not writable, put into user profile and show path...

                Log.Logger.Fatal(ex, "An unhandled exception occurred.");
                result = ExitCode.UnexpectedError;
            }

            // Prevent closing of window when debugging
            if (Debugger.IsAttached && Environment.UserInteractive)
            {
                Console.WriteLine();
                Console.Write("Press ENTER to exit...");
                Console.ReadLine();
            }

            // ExitCode:
            return (int)result;
        }
    }

    public enum ExitCode
    {
        UnexpectedError = -9,
        InvalidArgs = -2,
        NoArgsProvided = -1,
        Success = 0,
        FileNotFound = 1,
        DependenciesNotInstalled = 2,
    }

    public class AppSettings
    {
        public LoggingLevelSwitch LevelSwitch { get; } = new LoggingLevelSwitch(
#if DEBUG
            LogEventLevel.Verbose
#endif
        );

        internal bool ShouldSuppressConsoleLogs { get; set; } = false;

        public IDisposable SuppressConsoleLogs()
        {
            this.ShouldSuppressConsoleLogs = true;
            return new Disposable(()=>
            {
                this.ShouldSuppressConsoleLogs = false;
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
