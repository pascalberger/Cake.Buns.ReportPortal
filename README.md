# Installation
[![NuGet version](https://badge.fury.io/nu/Cake.Buns.ReportPortal.svg)](https://badge.fury.io/nu/Cake.Buns.ReportPortal)

Import **Cake.Buns.ReportPortal** NuGet package into your script for cleaning or/and merging ReportPortal launches.

# Usage example

```cake
#tool nuget:?package=NUnit.ConsoleRunner&version=3.9.0

//for using JSON in script
#addin nuget:?package=Cake.Json
#addin nuget:?package=Newtonsoft.Json&version=9.0.1

//for running NUnit tests in script
#addin nuget:?package=Cake.SoftNUnit3

//for cleaning and merging ReportPortal launches
#addin nuget:?package=Cake.Buns.ReportPortal
#addin nuget:?package=Buns.ReportPortal&loaddependencies=true

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var launchName = Argument("launch", "Daily");
var launchDescription = Argument<string>("description", "Daily run");

var rerunCount = Argument<int>("rerunCount", 1);

var filter = Argument<string>("filter", null);
var workers = Argument<int>("workers", 12);

var landscape = Argument<string>("landscape", null);

long startedAt = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
NUnit3Settings nunitSettings = new NUnit3Settings { NoResults = false, Workers = workers, Where = filter };

Service service = null;
FilterOption filterOption = null;

CleanOptions cleanOptions = new CleanOptions(removeSkipped:true);
MergeOptions mergeOptions = MergeOptions.Default;

bool debugMode = false;
//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define directories.
var buildDir = Directory("./pathToBinFolder") + Directory(configuration);

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
    {
        CleanDirectory(buildDir);
    });

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        NuGetRestore("./pathToSln");
    });

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
    { 
        MSBuild("./pathToSln", settings => settings.SetConfiguration(configuration));
    });

Task("Connect-Report-Portal")
    .IsDependentOn("Build")
    .Does(() =>
    {
        var addinsText = "../../../pathToBinFolder/" + configuration + "/ReportPortal.NUnitExtension.dll";
        System.IO.File.WriteAllText("tools/nunit.consolerunner.3.9.0/tools/ReportPortal.addins", addinsText);
    });


Task("Turn-up-ReportPortal-Configuration")
    .IsDependentOn("Connect-Report-Portal")
    .Does(() =>
    {
        var pathToReportPortalConfig = FilePath.FromString($"{buildDir}/ReportPortal.config.json");
        var config = ParseJsonFromFile(pathToReportPortalConfig);

        config["launch"]["name"] = launchName ?? config["launch"].Value<string>("name");
        config["launch"]["description"] = launchDescription ?? config["launch"].Value<string>("description");

        var url = new Uri(config["server"].Value<string>("url"));
        var name = config["launch"].Value<string>("name");

        var project = config["server"].Value<string>("project");
        var password = config["server"]["authentication"].Value<string>("uuid");

        debugMode = config["launch"].Value<bool>("debugMode");
        service = new Service(url, project, password);

        filterOption = new FilterOption
        {
           Filters = new List<Filter>
    	     {
              new Filter(FilterOperation.Equals, "name", name),
              new Filter(FilterOperation.GreaterThanOrEquals, "start_time", startedAt)
           },
           Paging = new Paging(1, short.MaxValue)
        };
        
        System.IO.File.WriteAllText(pathToReportPortalConfig.FullPath, config.ToString());
    });

Task("Run-Automation-Tests")
    .IsDependentOn("Turn-up-ReportPortal-Configuration")
    .Does(() =>
    {   
        SoftNUnit3($"{buildDir}/Example.Test.dll", nunitSettings);
    });

Task("Rerun-Automation-Tests")
    .IsDependentOn("Run-Automation-Tests")
    .Does(async () =>
    {
        var resultPaths = nunitSettings.Results?.Count != 0
            ? nunitSettings.Results.Select(r => r.FileName)
            : new[] { FilePath.FromString("TestResult.xml") };

        for (int i = 0; i < rerunCount; i++)
        {
            var failed = GetNUnit3NonPassedTests(resultPaths);

            if(failed.Count().Equals(0))
            {
                break;
            }

            var testList = CreateFile($"rerun{i}.txt");
            System.IO.File.WriteAllLines(testList.FullPath, failed);

            nunitSettings.TestList = testList;
            nunitSettings.Where = null;

            SoftNUnit3($"{buildDir}/Example.Test.dll", nunitSettings);
            var launch = await SmartMergeLaunchesAsync(service, filterOption, cleanOptions, mergeOptions, debugMode);
        }
    });

Task("Default")
    .IsDependentOn("Rerun-Automation-Tests");

RunTarget(target);
```

See [that](https://github.com/OlegYanushkevich/ReportPortal.Customization/blob/master/README.md) for learning merging and cleaning. 
