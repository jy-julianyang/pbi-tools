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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PbiTools.Utils
{
    public static class Resources
    {
        public static T GetEmbeddedResource<T>(string name, Func<Stream, T> transform, Assembly assembly = null)
        {
            using (var stream = GetEmbeddedResourceStream(name, assembly ?? Assembly.GetCallingAssembly()))
            {
                return transform(stream);
            }
        }

        public static string GetEmbeddedResourceString(string name, Encoding encoding = null, Assembly assembly = null)
        {
            using (var stream = GetEmbeddedResourceStream(name, assembly ?? Assembly.GetCallingAssembly()))
            using (var reader = new StreamReader(stream, encoding ?? Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        public static T GetEmbeddedResourceFromString<T>(string name, Func<string, T> transform, Assembly assembly = null) =>
            GetEmbeddedResource<T>(name, stream =>
            {
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return transform(reader.ReadToEnd());
                }
            }, assembly ?? Assembly.GetCallingAssembly());

        public static Stream GetEmbeddedResourceStream(string name, Assembly assembly = null)
        {
            var asm = assembly ?? Assembly.GetCallingAssembly();

            var resourceNames = asm.GetManifestResourceNames();
            var match = resourceNames.FirstOrDefault(n => n.EndsWith(name));
            if (match == null) throw new ArgumentException($"Embedded resource '{name}' not found.", nameof(name));

            return asm.GetManifestResourceStream(match);
        }

        public static bool ContainsName(string name, Assembly assembly = null) =>
            (assembly ?? Assembly.GetCallingAssembly()).GetManifestResourceNames().Contains(name);
    }
}
