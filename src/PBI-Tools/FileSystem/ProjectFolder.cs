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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Serilog;

namespace PbiTools.FileSystem
{
    /// <summary>
    /// Represents a sub-folder inside the PBIXPROJ directory, containing the artifacts
    /// for one PBIX part (e.g., /Mashup, /Report, etc.), or any nested sub-folder within.
    /// </summary>
    public interface IProjectFolder
    {
        /// <summary>
        /// Gets the full path of the folder.
        /// </summary>
        string BasePath { get; }

        /// <summary>
        /// Gets the directory name of the folder.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Returns <c>true</c> or <c>false</c>, indicating whether or not this folder exists.
        /// </summary>
        bool Exists();

        IProjectFolder GetSubfolder(params string[] segments);

        IEnumerable<IProjectFolder> GetSubfolders(string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly);

        IEnumerable<IProjectFile> GetFiles(string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Returns a reference to a file at the path specified relative to this folder instance.
        /// The file may or may not exist. The reference can be used to check whether the file exists, retrieve its contents, 
        /// overwrite its contents, or create a new file at the location.
        /// </summary>
        IProjectFile GetFile(string relativePath);
        
        /// <summary>
        /// Provides access to the <see cref="Stream"/> of a project file if it exists.
        /// Returns <c>true</c> if the file exists, otherwise <c>false</c>.
        /// </summary>
        bool TryReadFile(string path, Action<Stream> streamHandler);

        bool ContainsFile(string path);

        void DeleteFile(string path);
        
        /// <summary>
        /// Writes a binary file at the specified path by providing a <see cref="Stream"/> to write to.
        /// </summary>
        void WriteFile(string path, Action<Stream> onStreamAvailable);
        
        /// <summary>
        /// Writes a binary file at the specified path by providing a <see cref="TextWriter"/> to write to.
        /// </summary>
        void WriteText(string path, Action<TextWriter> onTextWriterAvailable);

        /// <summary>
        /// Creates or opens a file for writing UTF-8 encoded text at the specified relative path within this folder.
        /// If the file already exists, its contents are overwritten.
        /// </summary>
        TextWriter CreateTextWriter(string path);
    }


    [DebuggerDisplay("{BasePath}")]
    public class ProjectFolder : IProjectFolder
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<ProjectFolder>();

        private readonly ProjectRootFolder _root;
        private readonly DirectoryInfo _directoryInfo;

        public ProjectFolder(ProjectRootFolder root, string baseDir)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _directoryInfo = new DirectoryInfo(baseDir);
        }

        public string BasePath => _directoryInfo.FullName;
        public string Name => _directoryInfo.Name;

        private string GetFullPath(string path) => new FileInfo(Path.Combine(BasePath, SanitizePath(path))).FullName;

        public IProjectFolder GetSubfolder(params string[] segments)
        {
            if (segments == null) throw new ArgumentNullException(nameof(segments));
            return segments.Length == 0 
                ? this 
                : new ProjectFolder(_root, Path.Combine(this.BasePath, Path.Combine(segments)));
        }

        public bool TryReadFile(string path, Action<Stream> streamHandler)
        {
            var fullPath = GetFullPath(path);
            Log.Verbose("Attempting to read file: {Path}", fullPath);
            if (File.Exists(fullPath))
            {
                using (var stream = File.OpenRead(fullPath))
                {
                    streamHandler(stream);
                }
                Log.Debug("Successfully read file: {Path}", fullPath);
                return true;
            }
            else
            {
                Log.Debug("File not found: {Path}", fullPath);
                return false;
            }
        }

        public bool ContainsFile(string path)
        {
            var fullPath = GetFullPath(path);
            return File.Exists(fullPath);
        }

        public void DeleteFile(string path)
        {
            var fullPath = GetFullPath(path);
            File.Delete(fullPath);
            Log.Information("Removed file: {Path}", fullPath);
        }

        public void WriteFile(string path, Action<Stream> onStreamAvailable)
        {
            WriteFile(path, File.Create, onStreamAvailable);
        }

        public void WriteText(string path, Action<TextWriter> onTextWriterAvailable)
        {
            WriteFile(path, File.CreateText, onTextWriterAvailable);
        }

        public TextWriter CreateTextWriter(string path)
        {
            string fullPath = default;
            WriteFile(path, _fullPath => fullPath = _fullPath);

            return File.CreateText(fullPath);
        }

        private void WriteFile(string path, Action<string> writeFileImpl)
        {
            var fullPath = GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            if (Directory.Exists(fullPath))
            {
                Log.Verbose("Deleting directory at: {Path} as it conflicts with a new file to be created at the same location.", fullPath);
                Directory.Delete(fullPath, recursive: true);
            }

            Log.Verbose("Writing file: {Path}", fullPath);
            writeFileImpl(fullPath);

            _root.FileWritten(fullPath); // keeps track of files added or updated
        }

        private void WriteFile<T>(string path, Func<string, T> factory, Action<T> callback) where T : IDisposable
        {
            WriteFile(path, fullPath => 
            {
                using var writer = factory(fullPath);
                callback(writer);
            });
        }

        private static string SanitizePath(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (path.StartsWith("/") || path.StartsWith("\\")) return path.Substring(1);
            return path;
        }

        public bool Exists()
        {
            return Directory.Exists(this.BasePath);
        }

        public IEnumerable<IProjectFolder> GetSubfolders(string searchPattern, SearchOption searchOption) =>
            this.Exists()
            ? Directory.EnumerateDirectories(this.BasePath, searchPattern, searchOption).Select(dir => this.GetSubfolder(Path.GetFileName(dir)))
            : new IProjectFolder[0];

        public IProjectFile GetFile(string relativePath)
        {
            return new ProjectFile(this._root, GetFullPath(relativePath));
        }

        public IEnumerable<IProjectFile> GetFiles(string searchPattern, SearchOption searchOption) =>
            this.Exists()
            ? Directory.EnumerateFiles(this.BasePath, searchPattern, searchOption)
                .Select(path => new ProjectFile(this._root, path))
            : new IProjectFile[0];

    }

}
