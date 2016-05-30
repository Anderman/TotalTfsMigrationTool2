using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Xml;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace MoveTeamProject
{
    public class TeamProjectArea
    {
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>();
        private readonly Project _project;

        public TeamProjectArea(Project project)
        {
            _project = project;
        }

        public void AddProjectAreas(IEnumerable<string> areas)
        {
            var css = (ICommonStructureService) _project.Store.TeamProjectCollection.GetService(typeof (ICommonStructureService));
            var rootNodePath = $@"\{_project.Name}\Area";

            foreach (var path in areas.Select(x => x.RemovePartsFromPath("")).Select(area => rootNodePath + @"\" + area))
            {
                CreateNode(path, css);
            }
        }

        private static void CreateNode(string path, ICommonStructureService css)
        {
            var subPath = "";
            foreach (var s in path.Split('\\'))
            {
                if (subPath.Contains("Area")) // Area already exist. Only create sub area's
                {
                    var pathRoot = Cache.ContainsKey(subPath) ? Cache[subPath] : css.GetNodeFromPath(subPath).Uri;
                    if (!Cache.ContainsKey(subPath))
                        Cache.Add(subPath, pathRoot);
                    CreateNode(css, pathRoot, subPath, s);
                    //CreateNode(css, Cache[$@"{subPath}\{s}"], $@"{subPath}\{s}", "history");
                }
                subPath += (subPath != "" || s != "" ? "\\" : "") + s;
            }
        }

        private static void CreateNode(ICommonStructureService css, string pathRoot, string subpath, string folder)
        {
            var newPath = $@"{subpath}\{folder}";
            if (!Cache.ContainsKey(newPath))
            {
                try
                {
                    css.CreateNode(folder, pathRoot);
                    Cache.Add(newPath, css.GetNodeFromPath(newPath).Uri);
                }
                catch (CommonStructureSubsystemException )
                {
                    Cache.Add(newPath, css.GetNodeFromPath(newPath).Uri);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex.Message}");
                }
            }
        }

        public void Add(XmlNode tree)
        {
            var css = (ICommonStructureService) _project.Store.TeamProjectCollection.GetService(typeof (ICommonStructureService));
            var rootNodePath = $@"\{_project.Name}\Area";
            var pathRoot = css.GetNodeFromPath(rootNodePath);

            if (tree.FirstChild == null) return;

            var myNodeCount = tree.FirstChild.ChildNodes.Count;
            for (var i = 0; i < myNodeCount; i++)
            {
                var node = tree.ChildNodes[0].ChildNodes[i];
                try
                {
                    css.CreateNode(node.Attributes?["Name"].Value, pathRoot.Uri);
                }
                catch (Exception)
                {
                    //node already exists
                    continue;
                }

                if (node.FirstChild == null) continue;

                var nodePath = rootNodePath + "\\" + node.Attributes?["Name"].Value;
                GenerateSubAreas(node, nodePath, css);
            }
        }

        private static void GenerateSubAreas(XmlNode tree, string nodePath, ICommonStructureService css)
        {
            var path = css.GetNodeFromPath(nodePath);
            var nodeCount = tree.FirstChild.ChildNodes.Count;
            for (var i = 0; i < nodeCount; i++)
            {
                var node = tree.ChildNodes[0].ChildNodes[i];
                try
                {
                    css.CreateNode(node.Attributes?["Name"].Value, path.Uri);
                }
                catch (Exception)
                {
                    //node already exists
                    continue;
                }

                if (node.FirstChild == null) continue;

                var newPath = nodePath + "\\" + node.Attributes?["Name"].Value;
                GenerateSubAreas(node, newPath, css);
            }
        }
    }
}