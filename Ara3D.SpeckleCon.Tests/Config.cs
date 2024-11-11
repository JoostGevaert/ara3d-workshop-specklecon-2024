using Ara3D.Logging;
using Ara3D.Speckle.Data;
using Ara3D.Utils;

namespace Ara3D.SpeckleCon.Tests
{
    public static class Config
    {
        public static ILogger CreateLogger() => new Logger(LogWriter.ConsoleWriter, "");

        public static DirectoryPath InputFolder => PathUtil.GetCallerSourceFolder().RelativeFolder("..", "test-files");
        public static DirectoryPath OutputFolder => PathUtil.GetCallerSourceFolder().RelativeFolder("..", "test-output");

        // TODO: replace this with your own project id
        public static string TestProjectId = "5ff38fb6b1";
        
        public static string TestProjectUrl = SpeckleUtils.ProjectUrl(TestProjectId);
    }
}
