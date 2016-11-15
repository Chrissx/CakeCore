#tool "nuget:?package=GitReleaseNotes"
#tool "nuget:?package=GitVersion.CommandLine"
#tool "nuget:?package=gitlink"

// Based on -> http://www.michael-whelan.net/continuous-delivery-github-cake-gittools-appveyor/

//Main target to run the cake script
var target = Argument("target", "Default");

Information("Running target : {0}", target);

//Output directory. This will necessary to the AppVeyor
var outputDir = "./artifacts/";

//Solution file path
var solutionPath = "./SampleCore/SampleCore.sln";

//Main project path. We need is because of the versioning and packing to Nuget.
var mainProjectJson = "./SampleCore/src/SampleCore/project.json";


Task("Restore")
    .Does(() => {
        DotNetCoreRestore("SampleCore");
    });

Task("Build")
    .IsDependentOn("Restore")
    .Does(() => {
        MSBuild(solutionPath);
    });

Task("Test")
    .IsDependentOn("Build")
    .Does(() => {
        var directories = GetDirectories("./SampleCore/tests/*");
        
        Information("Test project count : {0}", directories.Count());

        foreach(var directory in directories)
        {
            Information("Start running tests in project: {0}", directory);

            DotNetCoreTest(directory.FullPath);

            Information("Test run finished in project: {0}", directory);
        }
    });

Task("Default")
     .IsDependentOn("Test");

RunTarget(target);