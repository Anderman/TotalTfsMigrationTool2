using System;
using log4net;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace MoveTeamProject
{
    public class TeamProjectQueryHierarchy
    {
        private readonly Project _projectDst;
        private static readonly ILog Logger = LogManager.GetLogger(typeof(TeamProjectWorkItem));

        public TeamProjectQueryHierarchy(Project projectDst)
        {
            _projectDst = projectDst;
        }

        public void Add(QueryHierarchy queryHierarchySrc, string sourceProjectName)
        {
            foreach (var queryItem in queryHierarchySrc)
            {
                var queryFolderSrc = (QueryFolder) queryItem;
                if (queryFolderSrc.Name == "Team Queries" || queryFolderSrc.Name == "Shared Queries")
                {
                    var queriesFolderDst = (QueryFolder) _projectDst.QueryHierarchy["Shared Queries"];
                    Add(queryFolderSrc, queriesFolderDst, sourceProjectName);
                }
            }
        }

        private void Add(QueryFolder queryFolderSrc, QueryFolder queryFolderDst, string projectNameSrc)
        {
            QueryItem newQueryFolderDst = null;
            foreach (var queryItemSrc in queryFolderSrc)
            {
                try
                {
                    if (queryItemSrc.GetType() == typeof(QueryFolder))
                    {
                        newQueryFolderDst = new QueryFolder(queryItemSrc.Name);
                        if (!queryFolderDst.Contains(queryItemSrc.Name))
                        {
                            queryFolderDst.Add(newQueryFolderDst);
                            _projectDst.QueryHierarchy.Save();
                        }
                        Add((QueryFolder)queryItemSrc, (QueryFolder)newQueryFolderDst, projectNameSrc);
                    }
                    else
                    {
                        var definitionSrc = (QueryDefinition)queryItemSrc;
                        var queryText = definitionSrc.QueryText.Replace(projectNameSrc, _projectDst.Name).Replace("User Story", "Product Backlog Item").Replace("Issue", "Impediment");

                        newQueryFolderDst = new QueryDefinition(queryItemSrc.Name, queryText);
                        if (!queryFolderDst.Contains(queryItemSrc.Name))
                        {
                            queryFolderDst.Add(newQueryFolderDst);
                            _projectDst.QueryHierarchy.Save();
                        }
                        else
                        {
                            Logger.WarnFormat("Query Definition {0} already exists", queryItemSrc);
                        }
                    }
                }
                catch (Exception ex)
                {
                    newQueryFolderDst?.Delete();
                    Logger.ErrorFormat("Error creating Query: {0} : {1}", queryItemSrc, ex.Message);
                }

            }

        }
    }
}