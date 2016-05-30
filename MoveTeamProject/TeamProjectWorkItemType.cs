using log4net;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace MoveTeamProject
{
    public class TeamProjectWorkItemType
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof (TeamProjectWorkItem));
        private static WorkItemTypeCollection _workItemTypes;

        public static WorkItemType GetWorkItemTypeDst(Project projectDst, string workItemTypeName)
        {
            _workItemTypes = _workItemTypes ?? projectDst.WorkItemTypes;
            if (_workItemTypes.Contains(workItemTypeName))
            {
                return _workItemTypes[workItemTypeName];
            }
            switch (workItemTypeName)
            {
                case "User Story":
                    return _workItemTypes["Product Backlog Item"];
                case "Issue":
                    return _workItemTypes["Impediment"];
                default:
                    Logger.InfoFormat("Work Item Type {0} does not exist in target TFS", workItemTypeName);
                    return null;
            }
        }

        public static string GetStateDst(Project projectDst, WorkItem workItem, string state)
        {
            _workItemTypes = _workItemTypes ?? projectDst.WorkItemTypes;

            if (!_workItemTypes.Contains("Product Backlog Item"))
                return state;

            //return scrum state
            switch (workItem.Type.Name)
            {
                case "Feature":
                    switch (state)
                    {
                        case "New":
                            return "New";
                        case "Active":
                            return "In Progress";
                        case "Resolved":
                            return "Done";
                        case "Closed":
                            return "Done";
                        default:
                            return workItem.State;
                    }
                case "User Story":
                    switch (state)
                    {
                        case "New":
                            return "New";
                        case "Active":
                            return "Approved";
                        case "Resolved":
                            return "Committed";
                        case "Closed":
                            return "Done";
                        default:
                            return workItem.State;
                    }
                case "Bug":
                    switch (state)
                    {
                        case "Active":
                            return "New";
                        case "Resolved":
                            return "Committed";
                        case "Closed":
                            return "Done";
                        default:
                            return workItem.State;
                    }
                case "Task":
                    switch (state)
                    {
                        case "New":
                            return "To Do";
                        case "Active":
                            return "In Progress";
                        case "Resolved":
                            return "Done";
                        case "Closed":
                            return "Done";
                        default:
                            return workItem.State;
                    }
                case "Issue":
                    switch (state)
                    {
                        case "New":
                            return "To Do";
                        case "Active":
                            return "Open";
                        case "Closed":
                            return "Closed";
                        default:
                            return workItem.State;
                    }
                default:
                    return state;
            }
        }
    }
}