#tool "nuget:?package=GitReleaseNotes"
#tool "nuget:?package=GitVersion.CommandLine"
#tool "nuget:?package=gitlink"

// Based on -> http://www.michael-whelan.net/continuous-delivery-github-cake-gittools-appveyor/

//Main target to run the cake script
var target = Argument("target", "Default");

//Output directory. This will necessary to the AppVeyor
var outputDir = "./artifacts/";

//Solution file path
var solutionPath = "./SampleCore/SampleCore.sln";

//Main project path. We need is because of the versioning and packing to Nuget.
var mainProjectJson = "./SampleCore/src/SampleCore/project.json";

Task("Clean")
    .Does(() => {
        if (DirectoryExists(outputDir))
        {
            DeleteDirectory(outputDir, recursive:true);
        }
        CreateDirectory(outputDir);
    });

Task("Restore")
    .Does(() => {
        DotNetCoreRestore("SampleCore");
    });

GitVersion versionInfo = null;
Task("Version")
    .Does(() => {
        GitVersion(new GitVersionSettings{
            UpdateAssemblyInfo = true,
            OutputType = GitVersionOutput.BuildServer
        });
        versionInfo = GitVersion(new GitVersionSettings{ OutputType = GitVersionOutput.Json });
        
        var updatedProjectJson = System.IO.File.ReadAllText(mainProjectJson)
            .Replace("1.0.0-*", versionInfo.NuGetVersion);

        System.IO.File.WriteAllText(mainProjectJson, updatedProjectJson);
    });

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Version")
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

 Task("Package")
     .IsDependentOn("Test")
     .Does(() => {
         GitLink("./", new GitLinkSettings { ArgumentCustomization = args => args.Append("-include Specify,Specify.Autofac") });

         GenerateReleaseNotes();

         PackageProject("SampleCore", mainProjectJson);

         if (AppVeyor.IsRunningOnAppVeyor)
         {
             foreach (var file in GetFiles(outputDir + "**/*"))
                 AppVeyor.UploadArtifact(file.FullPath);
         }
     });

 private void PackageProject(string projectName, string projectJsonPath)
 {
     var settings = new DotNetCorePackSettings
         {
             OutputDirectory = outputDir,
             NoBuild = true
         };

     DotNetCorePack(projectJsonPath, settings);

     System.IO.File.WriteAllLines(outputDir + "artifacts", new[]{
         "nuget:" + projectName + "." + versionInfo.NuGetVersion + ".nupkg",
         "nugetSymbols:" + projectName + "." + versionInfo.NuGetVersion + ".symbols.nupkg",
         "releaseNotes:releasenotes.md"
     });
 }    

 private void GenerateReleaseNotes()
 {
     var releaseNotesExitCode = StartProcess(
         @"tools\GitReleaseNotes\tools\gitreleasenotes.exe", 
         new ProcessSettings { Arguments = ". /o artifacts/releasenotes.md" });

     if (string.IsNullOrEmpty(System.IO.File.ReadAllText("./artifacts/releasenotes.md")))
         System.IO.File.WriteAllText("./artifacts/releasenotes.md", "No issues closed since last release");

     if (releaseNotesExitCode != 0) throw new Exception("Failed to generate release notes");
 }

 Task("Default")
     .IsDependentOn("Package");

RunTarget(target);