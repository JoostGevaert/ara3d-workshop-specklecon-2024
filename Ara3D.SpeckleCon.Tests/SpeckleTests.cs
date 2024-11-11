using Ara3D.Logging;
using Ara3D.Speckle.Data;
using Ara3D.Utils;
using Newtonsoft.Json;
using static Ara3D.SpeckleCon.Tests.Config;

namespace Ara3D.SpeckleCon.Tests
{
    public static class SpeckleTests
    {
        [Test, Explicit]
        public static void TryOpeningProject()
        {
            ProcessUtil.OpenUrl(TestProjectUrl);
        }

        [Test]
        public static void TestDefaultClientConnection()
        {
            var logger = CreateLogger();

            logger.Log($"This test requires that you have used the SPeckle Manager to set-up a default client");

            var client = SpeckleUtils.LoginDefaultClient(logger);
            if (client == null)
            {
                logger.Log("Failed to connect to default client");
            }
            else
            {
                logger.Log("Successfully connected to default client");
            }
            Assert.IsNotNull(client);
        }

        [Test]
        public static void ListModelsFromMyProject()
        {
            var logger = CreateLogger();

            logger.Log($"Getting models for project ID {TestProjectId}");

            if (TestProjectId == "5ff38fb6b1")
            {
                logger.LogError($"You did you change the `Config.TestProjectId` to your own project ID");
                logger.Log($"After downloading the code, the first thing you should do is go to the `Config` class");
                logger.Log($"and change the TestProjectId field to a project ID that you own.");
            }

            ListModelsFromProject(TestProjectId, logger);
        }

        public static void ListModelsFromProject(string projectId, ILogger logger)
        {
            var client = SpeckleUtils.LoginDefaultClient(logger);
            if (client == null)
            {
                logger.LogError("Unable to login to the default client");
                return;
            }
            logger.Log($"Connecting to project {SpeckleUtils.ProjectUrl(TestProjectId)}");
            var project = client.GetProject(TestProjectId);
            if (project == null)
            {
                logger.LogError($"Unable to loging to the project {TestProjectId}");
                return;
            }
            logger.Log($"Logged into project {project.name}");

            var models = client.GetModels(TestProjectId)?.ToList();
            if (models == null)
            {
                logger.Log($"Unable to retrieve models for project {TestProjectId}");
                return;
            }
            logger.Log($"Found {models.Count} models");
            foreach (var m in models)
                logger.Log($"  Model {m.name} has ID {m.id}");
        }

        [Test, Explicit]
        public static void DownloadMyModelsToJson()
        {
            var logger = CreateLogger();
            var client = SpeckleUtils.LoginDefaultClient(logger);
            foreach (var model in client.GetModels(TestProjectId))
            {
                logger?.Log($"Pulling Speckle representation of model from {model.id}");
                var b = client.PullModelFromId(TestProjectId, model.id, logger);
                
                logger?.Log($"Generating JSON from Speckle.Base object");
                var json = b.ToJson();
                
                var outputFilePath = OutputFolder.RelativeFile($"{model.id}.json");
                
                logger?.Log($"Wring JSON to {outputFilePath}");
                outputFilePath.WriteAllText(json);
            }
        }

        [Test, Explicit]
        public static void PushBunny()
        {
            var logger = CreateLogger();
            var f = InputFolder.RelativeFile("bunny.ply");
            logger?.Log($"Converting {f} to Speckle");

            /*
            var b = IfcFileToBase(f, logger);
            logger?.Log($"Conversion completed");
            var client = SpeckleUtils.LoginDefaultClient(logger);
            var result = client.PushModel(TestProjectId, f.GetFileName(), b, logger);
            logger?.Log(result);
            logger?.Log($"Pushed to Speckle at {TestProjectUrl}");
            */
        }

        [Test, Explicit]
        public static void PushHaus()
        {
            var logger = CreateLogger();

            var f = InputFolder.RelativeFile("AC20-FZK-Haus.ifc");
            var client = SpeckleUtils.LoginDefaultClient(logger);
            var projectId = TestProjectId;

            using MultipartFormDataContent formData = new();
            var fileStream = new FileStream(f, FileMode.Open, FileAccess.Read);
            using StreamContent streamContent = new(fileStream);
            formData.Add(streamContent, "files", f.GetFileName());
            var request = client.GQLClient.HttpClient
                .PostAsync(
                    new Uri($"{client.ServerUrl}api/file/ifc/stream/{projectId}/blob"),
                    formData
                ).Result;
            request.EnsureSuccessStatusCode();
            var responseString = request.Content.ReadAsStringAsync().Result;
            Console.WriteLine("RESPONSE - " + responseString);
        }
    }
}
