﻿using Microsoft.VisualStudio.TestWindow.Extensibility;
using PrestoCoverage.Interfaces;
using PrestoCoverage.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PrestoCoverage
{
    public static class PrestoCoverageCore
    {
        //var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
        //SolutionDirectory = System.IO.Path.GetDirectoryName(dte.Solution.FullName);

        public static CoverageRepository CoverageRepository { get; set; }

        public const string WatchFolder = @"c:\coverlet";
        public static FileSystemWatcher CoverageFileSystemWatcher;

        public static List<ITagReloader> TagSessions { get; set; }


        //public static string SolutionDirectory { get; set; }

        static PrestoCoverageCore()
        {
            CoverageRepository = new CoverageRepository();

            _coverageSession = new Dictionary<string, Coverlet.Core.Coverage>();
            TagSessions = new List<ITagReloader>();

            if (Directory.Exists(WatchFolder))
                CreateFileWatcher(WatchFolder, "*coverage.json");
        }


        public static void AddTagSession(ITagReloader tagReloader)
        {
            TagSessions.Add(tagReloader);
        }

        public static void RemoveTagSession(ITagReloader tagReloader)
        {
            TagSessions.Remove(tagReloader);
        }

        public static event EventHandler<OperationStateChangedEventArgs> TestExecutionFinished;


        private static Dictionary<string, Coverlet.Core.Coverage> _coverageSession;

        public static void OnTestExecutionStarting(object sender, OperationStateChangedEventArgs stateArgs)
        {
            _coverageSession = new Dictionary<string, Coverlet.Core.Coverage>();

            // You can get this lovely DLL in your visual studio directory Microsoft.VisualStudio.TestWindow.Core.dll
            // var g = ((Microsoft.VisualStudio.TestWindow.Controller.Request)stateArgs.Operation); 
            // If you know a better to get the actual "DLL path" without doing a Directory.Get files.. please let me know
            var testRequestConfiguration =
              stateArgs.Operation.GetType()
                  .GetProperty("Configuration", BindingFlags.NonPublic | BindingFlags.Instance)
                  .GetValue(stateArgs.Operation) as Microsoft.VisualStudio.TestWindow.Controller.TestRunConfiguration;

            if (testRequestConfiguration.Debug)
                return;

            foreach (var testDll in testRequestConfiguration.TestSources)
            {
                var _coverage = new Coverlet.Core.Coverage(testDll, new string[0], new string[0], new string[0], string.Empty);

                _coverage.PrepareModules();

                _coverageSession.Add(testDll, _coverage);
            }
        }

        public static void OnTestExecutionFinished(object sender, OperationStateChangedEventArgs stateArgs)
        {
            lock (_coverageSession)
            {
                var _sessions = _coverageSession.Keys.ToList();

                if (!_sessions.Any())
                    return;

                foreach (var sessionKey in _sessions)
                {
                    var result = _coverageSession[sessionKey].GetCoverageResult();

                    var covergeDetails = Loaders.CoverletLoader.LoadCoverage(result);

                    foreach (var cd in covergeDetails)
                        CoverageRepository.AddUpdateCoverage(cd.SourceFile, cd.CoveredFile, cd.LineVisits);
                }
            }

            reloadTaggers();
        }



        public static void CreateFileWatcher(string path, string filter)
        {
            CoverageFileSystemWatcher = new FileSystemWatcher();

            CoverageFileSystemWatcher.Path = path;
            CoverageFileSystemWatcher.IncludeSubdirectories = true;
            /* Watch for changes in LastAccess and LastWrite times, and 
               the renaming of files or directories. */
            CoverageFileSystemWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            // Only watch text files.
            CoverageFileSystemWatcher.Filter = filter;

            // Add event handlers.
            CoverageFileSystemWatcher.Changed += new FileSystemEventHandler(OnChanged);
            CoverageFileSystemWatcher.Created += new FileSystemEventHandler(OnChanged);
            CoverageFileSystemWatcher.Deleted += new FileSystemEventHandler(OnDeleted);

            // Begin watching.
            CoverageFileSystemWatcher.EnableRaisingEvents = true;
        }


        internal static void reloadTaggers()
        {
            List<ITagReloader> consilidator = new List<ITagReloader>();
            foreach (var tageSession in TagSessions)
            {
                try
                {
                    tageSession.ReloadTags();

                }
                catch (Exception)
                {
                    consilidator.Add(tageSession);
                }
            }

            if (consilidator.Any())
            {
                foreach (var c in consilidator)
                {
                    TagSessions.Remove(c);
                }
            }
        }

        internal static void OnChanged(object source, FileSystemEventArgs e)
        {
            var covergeDetails = Loaders.CoverletLoader.Load(e.FullPath);

            foreach (var cd in covergeDetails)
                CoverageRepository.AddUpdateCoverage(cd.SourceFile, cd.CoveredFile, cd.LineVisits);

            reloadTaggers();
        }

        internal static void OnDeleted(object source, FileSystemEventArgs e)
        {
            CoverageRepository.RemoveCoverage(e.FullPath);

            reloadTaggers();
        }


        //List<LineCoverageDetails> lcd = new List<LineCoverageDetails>();
        //foreach (var item in Directory.GetFiles(Settings.WatchFolder, "*coverage.json"))
        //{
        //    foreach (var lineCoverageDetail in Loaders.CoverletLoader.Load(item))
        //    {
        //        _coverage.AddUpdateCoverage(lineCoverageDetail.SourceFile, lineCoverageDetail.CoveredFile, lineCoverageDetail.LineVisits);
        //    }
        //}
        //CreateFileWatcher(Settings.WatchFolder, "*coverage.json");

    }

}
