/////////////////////////////////////////////////////////////////////
// ADDINS
/////////////////////////////////////////////////////////////////////
#addin "nuget:?package=Polly&version=5.0.6"
#addin "nuget:?package=NuGet.Core&version=2.14.0"
#addin "nuget:?package=SharpZipLib&version=0.86.0"
#addin "nuget:?package=Cake.Compression&version=0.1.1"

//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:https://dotnet.myget.org/F/nuget-build/?package=NuGet.CommandLine&version=4.3.0-beta1-2361&prerelease"

///////////////////////////////////////////////////////////////////////////////
// USINGS
///////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using Polly;
using NuGet;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var platform = Argument("platform", "AnyCPU");
var configuration = Argument("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// CONFIGURATION
///////////////////////////////////////////////////////////////////////////////

var MainRepo = "VitalElement/AvalonStudio.Toolchains.LDC";
var MasterBranch = "master";
var ReleasePlatform = "Any CPU";
var ReleaseConfiguration = "Release";

///////////////////////////////////////////////////////////////////////////////
// PARAMETERS
///////////////////////////////////////////////////////////////////////////////

var isLocalBuild = BuildSystem.IsLocalBuild;
var isRunningOnUnix = IsRunningOnUnix();
var isRunningOnWindows = IsRunningOnWindows();
var isRunningOnAppVeyor = BuildSystem.AppVeyor.IsRunningOnAppVeyor;
var isPullRequest = BuildSystem.AppVeyor.Environment.PullRequest.IsPullRequest;
var isMainRepo = StringComparer.OrdinalIgnoreCase.Equals(MainRepo, BuildSystem.AppVeyor.Environment.Repository.Name);
var isMasterBranch = StringComparer.OrdinalIgnoreCase.Equals(MasterBranch, BuildSystem.AppVeyor.Environment.Repository.Branch);
var isTagged = BuildSystem.AppVeyor.Environment.Repository.Tag.IsTag 
               && !string.IsNullOrWhiteSpace(BuildSystem.AppVeyor.Environment.Repository.Tag.Name);

///////////////////////////////////////////////////////////////////////////////
// VERSION
///////////////////////////////////////////////////////////////////////////////

var version = "4.0.0";

if (isRunningOnAppVeyor)
{
    if (isTagged)
    {
        // Use Tag Name as version
        version = BuildSystem.AppVeyor.Environment.Repository.Tag.Name;
    }
    else
    {
        // Use AssemblyVersion with Build as version
        version += "-build" + EnvironmentVariable("APPVEYOR_BUILD_NUMBER") + "-alpha";
    }
}

///////////////////////////////////////////////////////////////////////////////
// DIRECTORIES
///////////////////////////////////////////////////////////////////////////////

var artifactsDir = (DirectoryPath)Directory("./artifacts");
var zipRootDir = artifactsDir.Combine("zip");
var nugetRoot = artifactsDir.Combine("nuget");
var fileZipSuffix = ".zip";

private bool MoveFolderContents(string SourcePath, string DestinationPath)
{
   SourcePath = SourcePath.EndsWith(@"\") ? SourcePath : SourcePath + @"\";
   DestinationPath = DestinationPath.EndsWith(@"\") ? DestinationPath : DestinationPath + @"\";
 
   try
   {
      if (System.IO.Directory.Exists(SourcePath))
      {
         if (System.IO.Directory.Exists(DestinationPath) == false)
         {
            System.IO.Directory.CreateDirectory(DestinationPath);
         }
 
         foreach (string files in System.IO.Directory.GetFiles(SourcePath))
         {
            FileInfo fileInfo = new FileInfo(files);
            fileInfo.MoveTo(string.Format(@"{0}\{1}", DestinationPath, fileInfo.Name));
         }
 
         foreach (string drs in System.IO.Directory.GetDirectories(SourcePath))
         {
            System.IO.DirectoryInfo directoryInfo = new DirectoryInfo(drs);
            if (MoveFolderContents(drs, DestinationPath + directoryInfo.Name) == false)
            {
               return false;
            }
         }
      }
      return true;
   }
   catch (Exception ex)
   {
      return false;
   }
}

public class ArchiveDownloadInfo
{
    public string URL {get;set;}
    public string Name {get;set;}
    public FilePath DestinationFile {get; set;}
    public string Format {get; set;}
    public Action<DirectoryPath, ArchiveDownloadInfo> PostExtract {get; set;}
}

public class ToolchainDownloadInfo
{
    private DirectoryPath _artifactsDir;

    public ToolchainDownloadInfo(DirectoryPath artifactsDir)
    {
        _artifactsDir = artifactsDir;
        Downloads = new List<ArchiveDownloadInfo>();
    }

    public DirectoryPath BaseDir {get { return _artifactsDir.Combine(RID); } }

    public DirectoryPath ZipDir {get { return _artifactsDir.Combine("zip").Combine(RID); } }

    public string RID {get; set;}
    public List<ArchiveDownloadInfo> Downloads {get; set;}
    
}

var toolchainDownloads = new List<ToolchainDownloadInfo> 
{ 
    new ToolchainDownloadInfo (artifactsDir)
    { 
        RID = "win-x64", 
        Downloads = new List<ArchiveDownloadInfo>()
        { 
            new ArchiveDownloadInfo()
            { 
                Format = "zip", 
                DestinationFile = "ldc.zip", 
                URL =  "https://github.com/ldc-developers/ldc/releases/download/v1.3.0-beta2/ldc2-1.3.0-beta2-win64-msvc.zip",
                Name = "ldc2-1.3.0-beta2-win64-msvc",
                PostExtract = (curDir, info) =>
                {
                    MoveFolderContents(curDir.Combine(info.Name).ToString(), curDir.ToString());
                    DeleteDirectory(curDir.Combine(info.Name), true);                    
                }
            },
            new ArchiveDownloadInfo()
            {
                Format = "zip",
                DestinationFile = "gcc.zip",
                URL = "https://developer.arm.com/-/media/Files/downloads/gnu-rm/6_1-2017q1/gcc-arm-none-eabi-6-2017-q1-update-win32-zip.zip?product=GNU ARM Embedded Toolchain,ZIP,,Windows,6-2017-q1-update",
                Name= "gcc-arm-none-eabi-6-2017-q1-update",
                PostExtract = (curDir, info)=>
                {
                   
                }
            }
        }
    },
};

///////////////////////////////////////////////////////////////////////////////
// NUGET NUSPECS
///////////////////////////////////////////////////////////////////////////////
public NuGetPackSettings GetPackSettings(string rid)
{
    var nuspecNuGetBehaviors = new NuGetPackSettings()
    {
        Id = "AvalonStudio.Toolchains.LDC." + rid,
        Version = version,
        Authors = new [] { "VitalElement" },
        Owners = new [] { "Dan Walmsley" },
        LicenseUrl = new Uri("http://opensource.org/licenses/MIT"),
        ProjectUrl = new Uri("https://github.com/VitalElement/"),
        RequireLicenseAcceptance = false,
        Symbols = false,
        NoPackageAnalysis = true,
        Description = "LDC Toolchain for AvalonStudio",
        Copyright = "Copyright 2017",
        Tags = new [] { "clang", "AvalonStudio", "Toolchain" },
        Files = new []
        {
            new NuSpecContent { Source = "**", Target = "content/" },
        },

        BasePath = Directory("artifacts/" + rid + "/"),
        OutputDirectory = nugetRoot
    };

    return nuspecNuGetBehaviors;
}


///////////////////////////////////////////////////////////////////////////////
// INFORMATION
///////////////////////////////////////////////////////////////////////////////


///////////////////////////////////////////////////////////////////////////////
// TASKS
/////////////////////////////////////////////////////////////////////////////// 

Task("Clean")
.Does(()=>{    
    foreach(var tc in toolchainDownloads)
    {
        CleanDirectory(tc.BaseDir);   
        CleanDirectory(tc.ZipDir);
    }

    CleanDirectory(nugetRoot);
});

Task("Download-Toolchains")
.Does(()=>{
    foreach(var tc in toolchainDownloads)
    {
        foreach(var downloadInfo in tc.Downloads)
        {
            var fileName = tc.ZipDir.CombineWithFilePath(downloadInfo.DestinationFile);

            if(!FileExists(fileName))
            {
                DownloadFile(downloadInfo.URL, fileName);
            }
        }
    }
});

Task("Extract-Toolchains")
.Does(()=>{
    foreach(var tc in toolchainDownloads)
    {
        foreach(var downloadInfo in tc.Downloads)
        {
            var fileName = tc.ZipDir.CombineWithFilePath(downloadInfo.DestinationFile);
            var dest = tc.BaseDir;

            switch (downloadInfo.Format)
            {
                case "zip":
                ZipUncompress(fileName, dest);
                break;

                default:
                case "tar.bz2":
                case "tar.xz":
                Information("7z" + string.Format("x {0} -o{1}", fileName, dest));
                StartProcess("7z", new ProcessSettings{ Arguments = string.Format("x {0} -o{1}", fileName, dest) });
                break;
            }        

            if(downloadInfo.PostExtract != null)
            {
                downloadInfo.PostExtract(dest, downloadInfo);
            }
        }
    }
});

Task("Generate-NuGetPackages")
.Does(()=>{
    foreach(var tc in toolchainDownloads)
    {
        NuGetPack(GetPackSettings(tc.RID));
    }
});

Task("Publish-AppVeyorNuget")
    .IsDependentOn("Generate-NuGetPackages")        
    .WithCriteria(() => isMainRepo)
    .WithCriteria(() => isMasterBranch)    
    .Does(() =>
{
    var apiKey = EnvironmentVariable("APPVEYOR_NUGET_API_KEY");
    if(string.IsNullOrEmpty(apiKey)) 
    {
        throw new InvalidOperationException("Could not resolve MyGet API key.");
    }

    var apiUrl = EnvironmentVariable("APPVEYOR_ACCOUNT_FEED_URL");
    if(string.IsNullOrEmpty(apiUrl)) 
    {
        throw new InvalidOperationException("Could not resolve MyGet API url.");
    }

    foreach(var tc in toolchainDownloads)
    {
        var nuspec = GetPackSettings(tc.RID);
        var settings  = nuspec.OutputDirectory.CombineWithFilePath(string.Concat(nuspec.Id, ".", nuspec.Version, ".nupkg"));

        NuGetPush(settings, new NuGetPushSettings
        {
           Source = apiUrl,
            ApiKey = apiKey,
            Timeout = TimeSpan.FromMinutes(15)
        });
    }
});

Task("Default")    
    .IsDependentOn("Clean")
    .IsDependentOn("Download-Toolchains")
    .IsDependentOn("Extract-Toolchains")
    .IsDependentOn("Generate-NuGetPackages")
    .IsDependentOn("Publish-AppVeyorNuget");
RunTarget(target);
