using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml;
using log4net;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace MoveTeamProject
{
    public class TeamProjectIteration
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(TeamProjectWorkItem));
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>();
        private readonly Project _project;
        private string OldIterations { get; }

        public TeamProjectIteration(Project project, string oldIterations = "")
        {
            _project = project;
            OldIterations = oldIterations;
        }

        public void AddProjectIterations(IEnumerable<string> iterations, WorkItemStore storeSrc, string remove = "")
        {
            var cssDst = (ICommonStructureService)_project.Store.TeamProjectCollection.GetService(typeof(ICommonStructureService));
            var cssSrc = (ICommonStructureService)storeSrc.TeamProjectCollection.GetService(typeof(ICommonStructureService));

            var rootNodePath = $@"\{_project.Name}\Iteration" + OldIterations;
            foreach (var pathSrc in iterations)
            {
                var pathDst = rootNodePath + @"\" + pathSrc.RemovePartsFromPath(remove);
                var nodeSrcPath = pathSrc.ToNodeIterationPath();
                var nodeSrc = cssSrc.GetNodeFromPath(nodeSrcPath);
                Console.WriteLine(pathDst + nodeSrc.Name);
                CreateNodes(cssDst, pathDst, nodeSrc);
            }
        }


        private void CreateNodes(ICommonStructureService cssDst, string path, NodeInfo nodeSrc)
        {
            var nodePath = "";
            var nodes = path.Split('\\');
            for (var i = 4; i <= nodes.Length; i++)
            {
                nodePath = string.Join("\\", nodes.Take(i));
                if (!Cache.ContainsKey(nodePath))
                {
                    var nodeFolder = nodes.Skip(i - 1).Take(1).First();
                    var parentNodePath = string.Join("\\", nodes.Take(i - 1));

                    if (!Cache.ContainsKey(parentNodePath))
                        Cache.Add(parentNodePath, cssDst.GetNodeFromPath(parentNodePath).Uri);

                    var parentNodeUri = Cache[parentNodePath];
                    CreateNode(cssDst, parentNodeUri, nodeFolder);
                }
            }

            if (nodeSrc.StartDate == null)
                return;

            var iteration = new Iteration { attributes = new Attributes { startDate = nodeSrc.StartDate, finishDate = nodeSrc.FinishDate } };
            var url = getTeamProjectUrl(_project.Store.WebServiceUrl, _project.Store.TeamProjectCollection.Name);
            Task.Run(async () =>
            {
                var apiPath = nodePath.ToApiPath();
                await Webapi.PatchTfsObject(iteration, $"{url}/{_project.Name}/_apis/wit/classificationnodes/iterations/{apiPath}");
            }).Wait();
        }


        private static void CreateNode(ICommonStructureService cssDst, string parentNodeUri, string folder)
        {
            try
            {
                cssDst.CreateNode(folder, parentNodeUri);
            }
            catch (CommonStructureSubsystemException) //if Item already exists juist add this to the cache
            {
            }
            catch (Exception ex)
            {
                var msg = $"Error creating Node {folder} in {parentNodeUri}. {ex.Message}";
                Logger.Error(msg, ex.InnerException);
                Console.WriteLine(msg);
            }
        }

        private string getTeamProjectUrl(string webServiceUrl, string collectionName)
        {
            var teamCollectionUrl = "";
            if (collectionName.EndsWith("/") || collectionName.EndsWith("\\"))
                collectionName = collectionName.Substring(0, collectionName.Length - 1);
            var teamProjectCollectionName = collectionName.Substring(collectionName.LastIndexOfAny(new[] { '\\', '/' }) + 1);

            var strings = webServiceUrl.Split('/');
            for (var i = 0; i < strings.Length; i++)
            {
                if (strings[i] == teamProjectCollectionName)
                {
                    teamCollectionUrl = string.Join("/", strings, 0, i + 1);
                    break;
                }
            }
            if (teamCollectionUrl == "")
            {
                var msg = $"Cannot create service api. TeamCollection({teamProjectCollectionName}) not found in webserviceUrl{webServiceUrl}";
                Logger.Error(msg);
                throw new Exception(msg);
            }
            return $"{teamCollectionUrl}";
        }

        public void Add(XmlNode tree)
        {
            var css = (ICommonStructureService4)_project.Store.TeamProjectCollection.GetService(typeof(ICommonStructureService4));
            var rootNodePath = $@"\{_project.Name}\Iteration";
            var pathRoot = css.GetNodeFromPath(rootNodePath);

            var firstChild = tree.FirstChild;

            if (firstChild == null) return;

            CreateIterationNodes(firstChild, css, pathRoot);
        }

        private static void CreateIterationNodes(XmlNode node, ICommonStructureService4 css, NodeInfo pathRoot)
        {
            var myNodeCount = node.ChildNodes.Count;
            for (var i = 0; i < myNodeCount; i++)
            {
                var childNode = node.ChildNodes[i];
                NodeInfo createdNode;
                var name = childNode.Attributes?["Name"].Value;
                try
                {
                    var uri = css.CreateNode(name, pathRoot.Uri);
                    Console.WriteLine("NodeCreated:" + uri);
                    createdNode = css.GetNode(uri);
                }
                catch (Exception)
                {
                    //node already exists
                    createdNode = css.GetNodeFromPath(pathRoot.Path + @"\" + name);
                    //continue;
                }
                DateTime? startDateToUpdate = null;
                if (!createdNode.StartDate.HasValue)
                {
                    var startDate = childNode.Attributes?["StartDate"];
                    DateTime startDateParsed;
                    if (startDate != null && DateTime.TryParse(startDate.Value, out startDateParsed))
                        startDateToUpdate = startDateParsed;
                }
                DateTime? finishDateToUpdate = null;
                if (!createdNode.FinishDate.HasValue)
                {
                    DateTime finishDateParsed;
                    var finishDate = childNode.Attributes?["FinishDate"];
                    if (finishDate != null && DateTime.TryParse(finishDate.Value, out finishDateParsed))
                        finishDateToUpdate = finishDateParsed;
                }
                if (startDateToUpdate.HasValue || finishDateToUpdate.HasValue)
                    css.SetIterationDates(createdNode.Uri, startDateToUpdate, finishDateToUpdate);

                if (!node.HasChildNodes) continue;

                foreach (XmlNode subChildNode in childNode.ChildNodes)
                {
                    CreateIterationNodes(subChildNode, css, createdNode);
                }
            }
        }

        public class Iteration
        {
            // ReSharper disable once InconsistentNaming
            // Json parameter
            public Attributes attributes { get; set; }
        }

        public class Attributes
        {
            // ReSharper disable once InconsistentNaming
            // Json parameter
            public DateTime? startDate { get; set; }
            // ReSharper disable once InconsistentNaming
            // Json parameter
            public DateTime? finishDate { get; set; }
        }
    }
}