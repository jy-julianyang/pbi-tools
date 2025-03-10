/*
 * This file is part of the pbi-tools project <https://github.com/pbi-tools/pbi-tools>.
 * Copyright (C) 2018 Mathias Thierbach
 *
 * pbi-tools is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * pbi-tools is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * A copy of the GNU Affero General Public License is available in the LICENSE file,
 * and at <https://goto.pbi.tools/license>.
 */

using System;
using System.Diagnostics;
using PowerArgs;
using Serilog;

namespace PbiTools.Cli
{
    using Configuration;
    using Utils;

#if !DEBUG
    [ArgExceptionBehavior(ArgExceptionPolicy.StandardExceptionHandling)]  // PowerArgs will print the user friendly error message as well as the auto-generated usage documentation for the program.
#endif
    [ArgDescription(AssemblyVersionInformation.AssemblyProduct + " (" + AppSettings.Edition + "), " + AssemblyVersionInformation.AssemblyInformationalVersion + " - https://pbi.tools/")]
    [ArgProductVersion(AssemblyVersionInformation.AssemblyVersion)]
    [ArgProductName(AssemblyVersionInformation.AssemblyProduct)]
    [ArgCopyright(AssemblyVersionInformation.AssemblyCopyright)]
    [ApplyDefinitionTransforms]
    public partial class CmdLineActions
    {

        private static readonly ILogger Log = Serilog.Log.ForContext<CmdLineActions>();

        private readonly IDependenciesResolver _dependenciesResolver = DependenciesResolver.Default;
        private readonly AppSettings _appSettings;
        private readonly Stopwatch _stopWatch = Stopwatch.StartNew();

        public CmdLineActions() : this(Program.AppSettings)
        {
        }

        public CmdLineActions(AppSettings appSettings)
        {
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        }

        /// <remarks>
        /// See default usage template at <see href="https://github.com/adamabdelhamed/PowerArgs/blob/master/PowerArgs/ArgUsage.cs" />.
        /// </remarks>
        [HelpHook, ArgShortcut("-?"), ArgDescription("Shows this help")]
        public bool Help { get; set; }

        public static class NullableRevivers
        {
            [ArgReviver]
            public static Nullable<int> Int(string key, string value)
            {
                if (String.IsNullOrEmpty(value))
                    return null;
                if (int.TryParse(value, out var parsed))
                    return parsed;
                throw new ValidationArgException($"'{value}' is not an int.");
            }
        }
    }

}
