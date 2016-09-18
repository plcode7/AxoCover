﻿using AxoCover.Models.Data;
using AxoCover.Models.Extensions;
using EnvDTE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AxoCover.Models
{
  public class TestProvider : ITestProvider
  {
    private readonly ITestAssemblyScanner _testAssemblyScanner;

    public TestProvider(ITestAssemblyScanner testAssemblyScanner)
    {
      _testAssemblyScanner = testAssemblyScanner;
    }

    public async Task<TestSolution> GetTestSolutionAsync(Solution solution)
    {
      var testSolution = new TestSolution(solution.Properties.Item("Name").Value as string);

      var projects = solution.GetProjects();
      foreach (Project project in projects)
      {
        if (!project.IsDotNetUnitTestProject())
          continue;

        var outputFilePath = project.GetOutputDllPath();

        var testProject = new TestProject(testSolution, project.Name, outputFilePath);
      }

      await Task.Run(() =>
      {
        foreach (TestProject testProject in testSolution.Children.ToArray())
        {
          LoadTests(testProject);

          if (testProject.TestCount == 0)
          {
            testProject.Remove();
          }
        }
      });

      return testSolution;
    }

    private void LoadTests(TestProject testProject)
    {
      if (!File.Exists(testProject.OutputFilePath))
        return;

      var testItemPaths = _testAssemblyScanner.ScanAssemblyForTests(testProject.OutputFilePath);
      var testItems = new Dictionary<string, TestItem>()
      {
        { "", testProject }
      };

      foreach (var testPath in testItemPaths)
      {
        AddTestItem(testItems, CodeItemKind.Method, testPath);
      }
    }

    private static TestItem AddTestItem(Dictionary<string, TestItem> items, CodeItemKind itemKind, string itemPath)
    {
      var nameParts = itemPath.Split('.');
      var parentName = string.Join(".", nameParts.Take(nameParts.Length - 1));
      var itemName = nameParts[nameParts.Length - 1];

      TestItem parent;
      if (!items.TryGetValue(parentName, out parent))
      {
        if (itemKind == CodeItemKind.Method)
        {
          parent = AddTestItem(items, CodeItemKind.Class, parentName);
        }
        else
        {
          parent = AddTestItem(items, CodeItemKind.Namespace, parentName);
        }
      }

      TestItem item = null;
      switch (itemKind)
      {
        case CodeItemKind.Namespace:
          item = new TestNamespace(parent as TestNamespace, itemName);
          break;
        case CodeItemKind.Class:
          item = new TestClass(parent as TestNamespace, itemName);
          break;
        case CodeItemKind.Method:
          item = new TestMethod(parent as TestClass, itemName);
          break;
        default:
          throw new NotImplementedException();
      }
      items.Add(itemPath, item);

      return item;
    }
  }
}
