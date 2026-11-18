using System.Collections;
using Microsoft.Build.Framework;

namespace DailyPlants.Tests.TestDoubles;

internal sealed class FakeBuildEngine : IBuildEngine
{
    public List<BuildWarningEventArgs> Warnings { get; } = new();

    public bool ContinueOnError => false;

    public int LineNumberOfTaskNode => 0;

    public int ColumnNumberOfTaskNode => 0;

    public string ProjectFileOfTaskNode => "test.proj";

    public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => true;

    public void LogCustomEvent(CustomBuildEventArgs e)
    {
    }

    public void LogErrorEvent(BuildErrorEventArgs e)
    {
    }

    public void LogMessageEvent(BuildMessageEventArgs e)
    {
    }

    public void LogWarningEvent(BuildWarningEventArgs e) => Warnings.Add(e);
}
