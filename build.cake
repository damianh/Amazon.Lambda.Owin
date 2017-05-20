#addin "Cake.FileHelpers"

var target          = Argument("target", "Default");
var configuration   = Argument("configuration", "Release");
var artifactsDir    = Directory("./artifacts");
var solution        = "./src/AwsLambdaOwin.sln";
var buildNumber     = string.IsNullOrWhiteSpace(EnvironmentVariable("APPVEYOR_BUILD_NUMBER")) 
                        ? "0" 
                        : EnvironmentVariable("APPVEYOR_BUILD_NUMBER");
var version         = FileReadText("version.txt");
var commitSha       = string.IsNullOrWhiteSpace(EnvironmentVariable("APPVEYOR_REPO_COMMIT"))
                        ? ""
                        : EnvironmentVariable("APPVEYOR_REPO_COMMIT");

Task("Clean")
    .Does(() =>
{
    CleanDirectory(artifactsDir);
});

Task("RestorePackages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    DotNetCoreRestore(solution);
});

Task("Build")
    .IsDependentOn("RestorePackages")
    .Does(() =>
{
    var settings = new DotNetCoreBuildSettings
    {
        ArgumentCustomization = args => 
            args.Append("/p:Version=" + version + ";FileVersion=" + version + ";InformationalVersion=" + commitSha),
        Configuration = configuration
    };

    DotNetCoreBuild(solution, settings);
});

Task("RunTests")
    .IsDependentOn("Build")
    .Does(() =>
{
    var settings = new DotNetCoreTestSettings
    {
        Configuration = "Release",
        WorkingDirectory = "./src/AwsLambdaOwin.Tests"
    };

    DotNetCoreTest("AwsLambdaOwin.Tests.csproj", settings);
});

Task("Pack")
    .IsDependentOn("Build")
    .Does(() =>
{
    var packageVersion = version + "-ci" + buildNumber.PadLeft(5, '0');

    var settings = new DotNetCorePackSettings
    {
        ArgumentCustomization = args => args.Append("/p:Version=" + packageVersion),
        Configuration = "Release",
        OutputDirectory = "./artifacts/",
        NoBuild = true,
    };
    DotNetCorePack("./src/AwsLambdaOwin", settings);
});

Task("Default")
    .IsDependentOn("RunTests")
    .IsDependentOn("Pack");

RunTarget(target);