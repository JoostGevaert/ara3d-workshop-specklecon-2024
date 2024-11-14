using Ara3D.Logging;
using Ara3D.Speckle.Data;
using Ara3D.Utils;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Newtonsoft.Json;
using Objects.Geometry;
using Plato.DoublePrecision;
using Plato.Geometry.Graphics;
using Plato.Geometry.IO;
using Plato.Geometry.Scenes;
using Plato.Geometry.Speckle;
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
            var f = InputFolder.RelativeFile("bunny70k.ply");
            logger?.Log($"Converting {f} to Speckle");
            var mesh = PlyImporter.LoadMesh(f);
            var material = new Material(Colors.CornflowerBlue);
            var scene = new Scene();
            scene.Root.AddMesh(mesh, null, material);
            var b = scene.ToSpeckle();
            var client = SpeckleUtils.LoginDefaultClient(logger);
            var result = client.PushModel(TestProjectId, f.GetFileName(), b, logger);
            logger?.Log($"Pushed {f} to project at {TestProjectUrl} with result {result}");
        }

        [Test, Explicit("WARNING: This does not work. Working with Speckle team to figure out why not.")]
        public static async Task PushHaus()
        {
            var logger = CreateLogger();

            var f = InputFolder.RelativeFile("AC20-FZK-Haus.ifc");
            var client = SpeckleUtils.LoginDefaultClient(logger);
            var projectId = TestProjectId;

            using MultipartFormDataContent formData = new();
            var fileStream = new FileStream(f, FileMode.Open, FileAccess.Read);
            using StreamContent streamContent = new(fileStream);
            formData.Add(streamContent, "files", f.GetFileName());
            var request = await client.GQLClient.HttpClient
                .PostAsync(
                    new Uri($"{client.ServerUrl}/api/file/ifc/stream/{projectId}"),
                    formData
                ).ConfigureAwait(false);
            request.EnsureSuccessStatusCode();
            var responseString = request.Content.ReadAsStringAsync().Result;
            Console.WriteLine("RESPONSE - " + responseString);
        }

        [Test]
        public static void GetModelsByType()
        {
            var logger = CreateLogger();
            var client = SpeckleUtils.LoginDefaultClient(logger);
            foreach (var model in client.GetModels(TestProjectId))
            {
                logger?.Log($"Pulling Speckle representation of model from {model.id}");
                var b = client.PullModelFromId(TestProjectId, model.id, logger);

                var d = new Dictionary<string, (SpeckleObject, SceneNode)>();
                var scene = b.ToSpeckleObject().ToScene(d);

                // TODO:
                // Split the scene up according to the type of objects 

                var tmp = scene.GetNodes().ToList();

                var nodesWithMeshes = tmp.Where(n => n.Objects.Any(o => o is SceneMesh)).ToList();
                Console.WriteLine($"Found {nodesWithMeshes.Count} nodes with meshes");

                var distinctProps = tmp.SelectMany(n => n.GetProps().Select(p => p.Key)).Distinct().OrderBy(p => p)
                    .ToList();
                Console.WriteLine($"Found {distinctProps.Count} distinct properties");
                //foreach (var distinctProp in distinctProps) Console.WriteLine($"  {distinctProp}");

                var ifcTypes = tmp.SelectMany(n => n.GetProps().Where(p => p.Key == "ifc_type").Select(p => p.Value))
                    .Distinct().OrderBy(p => p).ToList();
                Console.WriteLine($"Found {ifcTypes.Count} distinct IFC types");
                foreach (var ifcType in ifcTypes)
                {
                    var filteredRoot = scene.Root.Filter(n =>
                        n.GetProps().Any(kv => kv.Key == "ifc_type" && kv.Value == ifcType));
                    var totalChildren = filteredRoot.GetSelfAndDescendants().Count();
                    Console.WriteLine($"{ifcType} has {totalChildren}");
                }
            }
        }

        public static bool Clashes(TriangleMesh3D m1, TriangleMesh3D m2, bool useBoundingBoxOnly)
        {
            var box1 = m1.Points.Bounds();
            var box2 = m2.Points.Bounds();

            if (!box1.Overlaps(box2))
                return false;

            if (useBoundingBoxOnly)
                return true;
        
            foreach (var tri1 in m1.Triangles)
            {
                foreach (var tri2 in m2.Triangles)
                {
                    if (tri1.Intersects(tri2))
                        return true;
                }
            }
    
            return false;
        }

        
        [Test]
        public static void TestClashes()
        {
            var logger = CreateLogger();
            var client = SpeckleUtils.LoginDefaultClient(logger);
            foreach (var model in client.GetModels(TestProjectId))
            {
                logger?.Log($"Pulling Speckle representation of model from {model.id}");
                var b = client.PullModelFromId(TestProjectId, model.id, logger);

                var d = new Dictionary<string, (SpeckleObject, SceneNode)>();
                var scene = b.ToSpeckleObject().ToScene(d);

                var meshNodes = scene.GetNodesWithMeshes().ToList();
                logger?.Log($"Found {meshNodes.Count} nodes with meshes");

                var meshAndNodeList = meshNodes.SelectMany(n => n.GetMeshes().Select(m => (m.Mesh.Deform(n.Transform), n))).ToList();
                logger?.Log($"Found total of {meshAndNodeList.Count} meshes");

                var boundsList = meshAndNodeList.Select(m => m.Item1.Points.Bounds()).ToList();

                if (meshAndNodeList.Count <= 1)
                {
                    logger?.Log("Not enough meshes to compare.");
                }

                for (var i = 0; i < meshAndNodeList.Count; ++i)
                {
                    for (var j = i + 1; j < meshAndNodeList.Count; ++j)
                    {
                        var m1 = meshAndNodeList[i];
                        var m2 = meshAndNodeList[j];

                        if (boundsList[i].Overlaps(boundsList[j]))
                        //if (Clashes(m1.Item1, m2.Item1, true))
                        {
                            logger?.Log($"Found clash between mesh {i}({m1.Item2.Name}:{m1.Item2.Id}) and mesh {j}({m2.Item2.Name}:{m2.Item2.Id})");
                        }
                    }
                }
            }
        }
    }
}
