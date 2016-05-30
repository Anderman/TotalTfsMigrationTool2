using System;
using System.Collections;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml;
using log4net;
using log4net.Config;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Proxy;

namespace MoveTeamProject
{
    internal class Program
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Program));
        private static readonly string SourceUri = ConfigurationManager.AppSettings["SourceUri"];
        private static readonly string DestUri = ConfigurationManager.AppSettings["DestUri"];
        private static readonly string ConnectionString = ConfigurationManager.ConnectionStrings["DefaultCollection"]?.ConnectionString;

        private static void Main(string[] args)
        {
            // Connect to Team Foundation Server
            //     Server is the name of the server that is running the application tier for Team Foundation.
            //     Port is the port that Team Foundation uses. The default port is 8080.
            //     VDir is the virtual path to the Team Foundation application. The default path is tfs.

            XmlConfigurator.Configure();
            Logger.Info("Start");
            var tfsUriSrc = args.Length < 1 ? new Uri(SourceUri) : new Uri(args[0]);
            var tfsUriDst = args.Length < 2 ? new Uri(DestUri) : new Uri(args[1]);

            if (tfsUriSrc.Segments.Length == 5 && tfsUriDst.Segments.Length == 4)
                MoveAreaToAnotherProject(tfsUriSrc, tfsUriDst);
            if (tfsUriSrc.Segments.Length == 4 && tfsUriDst.Segments.Length == 4)
                MoveTeamProject(tfsUriSrc, tfsUriDst);
            else if (tfsUriSrc.Segments.Length == 3 && tfsUriDst.Segments.Length == 4)
                MultipleToSingleProject(tfsUriSrc, tfsUriDst);
            else
                Console.WriteLine();
        }

        private static void MoveAreaToAnotherProject(Uri tfsUriSrc, Uri tfsUriDst)
        {
            var area = tfsUriSrc.AbsolutePath.Substring(tfsUriSrc.AbsolutePath.LastIndexOfAny(new[] { '\\', '/' }) + 1);
            tfsUriSrc = new Uri(tfsUriSrc.AbsoluteUri.Replace($"/{area}", ""));
            var storeSrc = GetStore(tfsUriSrc);
            var projectSrc = GetTeamProject(tfsUriSrc);
            var projectDst = GetTeamProject(tfsUriDst);

            var workItemsSrc = projectSrc.Store.Query($"SELECT * FROM WorkItems WHERE [System.TeamProject] = '{projectSrc.Name}'"); //Get Workitems from source tfs 
            var areaPathSrc = $@"{projectSrc.Name}\{area}\";
            var workItems = workItemsSrc.Cast<WorkItem>().Where(x => x.AreaPath.StartsWith(areaPathSrc)).Select(x => new { x.Project.Name, x.AreaPath, x.IterationPath }).ToList();
            var areaPaths = workItems.Select(x => x.AreaPath.Replace($@"{projectSrc.Name}\", "")).Distinct();
            var iterationPaths = workItems.Select(x => x.IterationPath).Distinct().OrderBy(x => x);
            new TeamProjectArea(projectDst).AddProjectAreas(areaPaths); //Copy Areas
            new TeamProjectIteration(projectDst).AddProjectIterations(iterationPaths, storeSrc, $@"{projectSrc.Name}\"); //Copy iterations from source
            var sql = Sql.MoveWorkItems.Replace("@Area", area).Replace("@projectSrc", projectSrc.Name).Replace("@ProjectDst", projectDst.Name);
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                SqlCommand command = new SqlCommand(sql, connection);
                command.Connection.Open();
                command.ExecuteNonQuery();
            }
        }

        private static void MultipleToSingleProject(Uri tfsUriSrc, Uri tfsUriDst)
        {
            Console.WriteLine("Migrate from multiple project to single project with feature team projects");
            var storeSrc = GetStore(tfsUriSrc);
            var projectDst = GetTeamProject(tfsUriDst);
            Console.WriteLine("Reading all workitems...");
            var workItemsSrc = storeSrc.Query("SELECT * FROM WorkItems ORDER BY ID"); //Get Workitems from source tfs 
            Console.WriteLine("Write all attachments to disk...");
            SaveAttachments(workItemsSrc);
            var workItems = workItemsSrc.Cast<WorkItem>().Select(x => new { x.Project.Name, x.AreaPath, x.IterationPath }).ToList();
            var projects = workItems.Select(x => x.Name).Distinct();
            var areaPaths = workItems.Select(x => x.AreaPath).Distinct();
            var iterationPaths = workItems.Select(x => x.IterationPath).Distinct().OrderBy(x => x);
            var iterationPaths2 = iterationPaths.Select(x => @"Old Iterations\" + x);

            Console.WriteLine("Creating new areas structure...");
            new TeamProjectArea(projectDst).AddProjectAreas(areaPaths); //Copy Areas
            Console.WriteLine("Creating new Iteration structure...");
            new TeamProjectIteration(projectDst, "\\Old Iterations").AddProjectIterations(iterationPaths, storeSrc); //Copy iterations from source

            Console.WriteLine("Copy fields from all source workItemsDefinitions to destination project");

            new TeamProjectWorkItemDefinition(projectDst).AddMissingFields(storeSrc, projects);
            Console.WriteLine("Copy workItems to new area and iteration structure");
            new TeamProjectWorkItem(projectDst, "\\Old Iterations").AddAndUpdate(storeSrc, workItemsSrc); //Copy Workitems
        }

        private static void MoveTeamProject(Uri tfsUriSrc, Uri tfsUriDst)
        {
            var projectSrc = GetTeamProject(tfsUriSrc);
            var projectDst = GetTeamProject(tfsUriDst);

            var workItemsSrc = projectSrc.Store.Query($"SELECT * FROM WorkItems WHERE [System.TeamProject] = '{projectSrc.Name}'"); //Get Workitems from source tfs 
            SaveAttachments(workItemsSrc);

            var iterations = GetIterations(projectSrc); //Get Areas from source tfs 
            new TeamProjectIteration(projectDst).Add(iterations); //Copy Iterations
            RefreshCache(projectDst);

            var areas = GetAreas(projectSrc); //Get Iterations from source tfs 
            new TeamProjectArea(projectDst).Add(areas); //Copy Areas
            RefreshCache(projectDst);

            new TeamProjectWorkItemDefinition(projectDst).AddMissingFields(projectSrc);
            new TeamProjectQueryHierarchy(projectDst).Add(projectSrc.QueryHierarchy, projectSrc.Name); //Copy Queries
            new TeamProjectWorkItem(projectDst).AddAndUpdate(projectSrc, workItemsSrc, new Hashtable()); //Copy Workitems

        }

        private static XmlNode GetIterations(Project project)
        {
            var css = (ICommonStructureService)project.Store.TeamProjectCollection.GetService(typeof(ICommonStructureService));
            var projectInfo = css.GetProjectFromName(project.Name);
            var nodes = css.ListStructures(projectInfo.Uri);

            return css.GetNodesXml(new[] { nodes.Single(n => n.StructureType == "ProjectLifecycle").Uri }, true).ChildNodes[0];
        }

        private static XmlNode GetAreas(Project project)
        {
            var css = (ICommonStructureService)project.Store.TeamProjectCollection.GetService(typeof(ICommonStructureService));
            var projectInfo = css.GetProjectFromName(project.Name);
            var nodes = css.ListStructures(projectInfo.Uri);

            return css.GetNodesXml(new[] { nodes.Single(n => n.StructureType == "ProjectModelHierarchy").Uri }, true).ChildNodes[0];
        }

        private static WorkItemStore GetStore(Uri tfsUri)
        {
            var teamProjectName = tfsUri.Segments.Length == 4 ? tfsUri.Segments.Last() : "";
            var server = tfsUri.AbsoluteUri.Substring(0, tfsUri.AbsoluteUri.Length - tfsUri.AbsolutePath.Length);

            var collectionPath = tfsUri.AbsolutePath.Substring(0, tfsUri.AbsolutePath.Length - teamProjectName.Length);
            var collectionUri = new Uri(server + collectionPath);
            var teamProjectCollection = new TfsTeamProjectCollection(collectionUri);
            return new WorkItemStore(teamProjectCollection, WorkItemStoreFlags.BypassRules);
        }

        private static Project GetTeamProject(Uri tfsUri)
        {
            var teamProjectName = tfsUri.Segments.Last();
            var store = GetStore(tfsUri);
            return store.Projects[teamProjectName];
        }

        private static void SaveAttachments(WorkItemCollection workItemCollection)
        {
            CreateEmptyFolder(@"Attachments");

            var webClient = new WebClient { UseDefaultCredentials = true };

            foreach (var wi in workItemCollection.Cast<WorkItem>().Where(x => x.AttachedFileCount > 0))
            {
                foreach (Attachment att in wi.Attachments)
                {
                    var path = $@"Attachments\{wi.Id}";
                    var filename = $@"{path}\{att.Id}_{att.Name}";
                    Directory.CreateDirectory(path);
                    try
                    {
                        webClient.DownloadFile(att.Uri, filename);
                    }
                    catch (Exception)
                    {
                        Logger.Info($"Error downloading attachment for work item : {wi.Id} Type: {wi.Type.Name}");
                    }
                }
            }
        }

        private static void RefreshCache(Project projectDst)
        {
            var css = projectDst.Store.TeamProjectCollection.GetService<ICommonStructureService>();
            var server = projectDst.Store.TeamProjectCollection.GetService<WorkItemServer>();
            server.SyncExternalStructures(WorkItemServer.NewRequestId(), css.GetProjectFromName(projectDst.Name).Uri);
            projectDst.Store.RefreshCache();
        }

        private static void CreateEmptyFolder(string folderName)
        {
            Directory.CreateDirectory(folderName);
            EmptyFolder(new DirectoryInfo(folderName));
        }

        /*Delete all subfolders and files in given folder*/

        private static void EmptyFolder(DirectoryInfo directoryInfo)
        {
            foreach (var file in directoryInfo.GetFiles())
            {
                file.Delete();
            }
            foreach (var subfolder in directoryInfo.GetDirectories())
            {
                EmptyFolder(subfolder);
                subfolder.Delete();
            }
        }
    }
}