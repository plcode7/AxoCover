﻿using AxoCover.Models.Data;
using AxoCover.Models.Data.CoverageReport;
using AxoCover.Models.Data.TestReport;
using AxoCover.Models.Events;
using AxoCover.Models.Extensions;
using AxoCover.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace AxoCover.Models
{
  public abstract class TestRunner : ITestRunner
  {
    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;

    public event EventHandler TestsStarted;
    public event TestExecutedEventHandler TestExecuted;
    public event LogAddedEventHandler TestLogAdded;
    public event TestFinishedEventHandler TestsFinished;
    public event EventHandler TestsFailed;
    public event EventHandler TestsAborted;

    private const string _runnerName = @"Runner\OpenCover\OpenCover.Console.exe";
    protected readonly static string _runnerPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), _runnerName);
    private Task _testTask;
    protected bool _isAborting;

    public bool IsBusy
    {
      get
      {
        return _testTask != null;
      }
    }

    public Task RunTestsAsync(TestItem testItem, string testSettings = null)
    {
      if (IsBusy)
      {
        throw new InvalidOperationException("The test runner is busy. Please wait for tests to complete or abort.");
      }

      _testTask = Task.Run(() =>
      {
        try
        {
          OnTestLogAdded(Resources.CoverageExecutorStarted);
          RunTests(testItem, testSettings);
        }
        finally
        {
          _testTask = null;
          _isAborting = false;
          OnTestLogAdded(Resources.CoverageExecutorFinished);
        }
      });
      TestsStarted?.Invoke(this, EventArgs.Empty);
      return _testTask;
    }

    protected abstract void RunTests(TestItem testItem, string testSettings);

    protected void OnTestLogAdded(string text)
    {
      _dispatcher.BeginInvoke(() => TestLogAdded?.Invoke(this, new LogAddedEventArgs(text)));
    }

    protected void OnTestExecuted(string path, TestState outcome)
    {
      _dispatcher.BeginInvoke(() => TestExecuted?.Invoke(this, new TestExecutedEventArgs(path, outcome)));
    }

    protected void OnTestsFinished(CoverageSession coverageReport, TestRun testReport)
    {
      if (_isAborting)
      {
        _dispatcher.BeginInvoke(() => TestsAborted?.Invoke(this, EventArgs.Empty));
      }
      else if (coverageReport != null && testReport != null)
      {
        _dispatcher.BeginInvoke(() => TestsFinished?.Invoke(this, new TestFinishedEventArgs(coverageReport, testReport)));
      }
      else
      {
        _dispatcher.BeginInvoke(() => TestsFailed?.Invoke(this, EventArgs.Empty));
      }
    }

    public Task AbortTestsAsync()
    {
      if (IsBusy)
      {
        _isAborting = true;
        AbortTests();
      }

      return _testTask ?? new TaskCompletionSource<object>().Task;
    }

    protected abstract void AbortTests();

    protected string GetSettingsBasedArguments(IEnumerable<string> codeAssemblies, IEnumerable<string> testAssemblies)
    {
      var arguments = string.Empty;

      if (!string.IsNullOrWhiteSpace(Settings.Default.ExcludeAttributes))
      {
        arguments += $" \"-excludebyattribute:{Settings.Default.ExcludeAttributes}\"";
      }

      if (!string.IsNullOrWhiteSpace(Settings.Default.ExcludeFiles))
      {
        arguments += $" \"-excludebyfile:{Settings.Default.ExcludeFiles}\"";
      }

      if (!string.IsNullOrWhiteSpace(Settings.Default.ExcludeDirectories))
      {
        arguments += $" \"-excludedirs:{Settings.Default.ExcludeDirectories}\"";
      }

      var filters = string.Empty;
      if (Settings.Default.IsIncludingSolutionAssemblies)
      {
        filters += GetAssemblyList(codeAssemblies);

        if (!Settings.Default.IsExcludingTestAssemblies)
        {
          filters += GetAssemblyList(testAssemblies);
        }
      }
      else if (!string.IsNullOrWhiteSpace(Settings.Default.Filters))
      {
        filters += Settings.Default.Filters;

        if (Settings.Default.IsExcludingTestAssemblies)
        {
          filters += GetAssemblyList(testAssemblies, false);
        }
      }

      if (!string.IsNullOrWhiteSpace(filters))
      {
        arguments += $" \"-filter:{filters}\"";
      }

      return arguments + " -hideskipped:All ";
    }

    private static string GetAssemblyList(IEnumerable<string> assemblies, bool isInclusive = true)
    {
      return string.Join(" ", assemblies.Select(p => (isInclusive ? "+" : "-") + "[" + p + "]*"));
    }
  }
}
