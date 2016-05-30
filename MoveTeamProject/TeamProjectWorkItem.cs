using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using log4net;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace MoveTeamProject
{
    public class TeamProjectWorkItem
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof (TeamProjectWorkItem));
        private readonly Project _projectDst;
        private Dictionary<int, int> _itemMap;
        public string OldIterations { get; }

        public TeamProjectWorkItem(Project projectDst, string oldIterations="")
        {
            _projectDst = projectDst;
            OldIterations = oldIterations;
        }


        /* Copy work items to project from work item collection */

        public void AddAndUpdate(WorkItemStore storeSrc, WorkItemCollection workItemsSrc)
        {
            _itemMap = _projectDst.Store.Query($"SELECT * FROM WorkItems WHERE [System.TeamProject] = '{_projectDst.Name}'").Cast<WorkItem>().Where(x => x.Fields["OrgId"].Value != null).ToDictionary(x => (int) x.Fields["OrgId"].Value, x => x.Id);
            var workItemsDst = _projectDst.Store.Query("SELECT ID FROM WorkItems").Cast<WorkItem>();

            var workItemIdNew = workItemsDst.Any() ? workItemsDst.Max(x => x.Id) : 1;
            var newWorkItemsSrc = new List<WorkItem>();
            var storeDst = new WorkItemStore(_projectDst.Store.TeamProjectCollection, WorkItemStoreFlags.BypassRules);

            foreach (WorkItem workItemSrc in workItemsSrc)
            {
                while (workItemSrc.Id%100 != workItemIdNew%100 || workItemSrc.Id > workItemIdNew)
                {
                    workItemIdNew = SkipWorkItem();
                }
                var workItemTypeDst = TeamProjectWorkItemType.GetWorkItemTypeDst(_projectDst, workItemSrc.Type.Name);

                var workItemDst = _itemMap.ContainsKey(workItemSrc.Id)
                    ? storeDst.GetWorkItem(_itemMap[workItemSrc.Id])
                    : new WorkItem(workItemTypeDst);

                workItemDst.Open();
                CopyFields(workItemSrc.Project.Name, new Hashtable(), workItemSrc, workItemDst, $@"\{workItemSrc.Project.Name.RemovePartsFromPath("")}");
                UploadAttachments(workItemSrc, workItemDst);

                if (TrySave(workItemSrc, workItemDst))
                {
                    workItemIdNew = workItemDst.Id + 1;
                    AddToItemMap(newWorkItemsSrc, workItemSrc, workItemDst);
                    //TrySave(workItemSrc, workItemDst);
                    AddStateChangeInfo(workItemSrc, workItemDst);
                    Logger.Info($"Work item {workItemSrc.Id},{workItemDst.Id},{workItemSrc.Title} migrated.");
                    Console.WriteLine($"Src={workItemSrc.Id},Dst={workItemDst.Id}");
                }
            }

            WriteMaptoFile("store");
            CreateLinks(newWorkItemsSrc);
        }

        private int SkipWorkItem()
        {
            var workItemDst = new WorkItem(TeamProjectWorkItemType.GetWorkItemTypeDst(_projectDst, "Bug"));
            TrySave(workItemDst, workItemDst);
            workItemDst.Store.DestroyWorkItems(new List<int> {workItemDst.Id});
            return workItemDst.Id + 1;
        }

        public void AddAndUpdate(Project projectSrc, WorkItemCollection workItemsSrc, Hashtable fieldMapAll)
        {
            ReadItemMap(projectSrc.Name);
            var dstIds = _projectDst.Store.Query($"SELECT ID,medella.OrgID FROM WorkItems WHERE [System.TeamProject] = '{_projectDst.Name}'").Cast<WorkItem>().Select(x => x.Id).ToList();
            var newWorkItemsSrc = new List<WorkItem>();
            var storeDst = new WorkItemStore(_projectDst.Store.TeamProjectCollection, WorkItemStoreFlags.BypassRules);
            foreach (WorkItem workItemSrc in workItemsSrc)
            {
                var workItemTypeDst = TeamProjectWorkItemType.GetWorkItemTypeDst(_projectDst, workItemSrc.Type.Name);

                var workItemDst = _itemMap.ContainsKey(workItemSrc.Id) && dstIds.Contains(_itemMap[workItemSrc.Id])
                    ? storeDst.GetWorkItem(_itemMap[workItemSrc.Id])
                    : new WorkItem(workItemTypeDst);

                workItemDst.Open();
                CopyFields(projectSrc.Name, fieldMapAll, workItemSrc, workItemDst, "");

                if (TrySave(workItemSrc, workItemDst))
                {
                    AddToItemMap(newWorkItemsSrc, workItemSrc, workItemDst);
                    UploadAttachments(workItemSrc, workItemDst);
                    TrySave(workItemSrc, workItemDst);
                    AddStateChangeInfo(workItemSrc, workItemDst);
                    Logger.Info($"Work item {workItemSrc.Id},{workItemDst.Id},{workItemSrc.Title} migrated.");
                }
            }
            WriteMaptoFile(projectSrc.Name);
            CreateLinks(newWorkItemsSrc);
        }

        private void AddToItemMap(List<WorkItem> newWorkItemsSrc, WorkItem workItemSrc, WorkItem workItemDst)
        {
            if (!_itemMap.ContainsKey(workItemSrc.Id) || _itemMap[workItemSrc.Id] != workItemDst.Id)
            {
                if (_itemMap.ContainsKey(workItemSrc.Id))
                {
                    _itemMap[workItemSrc.Id] = workItemDst.Id;
                }
                else
                {
                    _itemMap.Add(workItemSrc.Id, workItemDst.Id);
                }
                newWorkItemsSrc.Add(workItemSrc);
            }
        }

        private static bool TrySave(WorkItem workItemSrc, WorkItem workItemDst)
        {
            var errorList = workItemDst.Validate();
            foreach (Field item in errorList)
            {
                Console.WriteLine($"Work item {workItemSrc.Id} Validation Error in field: {item.Name}  : {workItemDst.Fields[item.Name].Value}");
                Logger.Info($"Work item {workItemSrc.Id} Validation Error in field: {item.Name}  : {workItemDst.Fields[item.Name].Value}");
            }
            if (errorList.Count == 0)
                workItemDst.Save();
            workItemDst.Close();
            workItemDst.Open();

            return errorList.Count == 0;
        }

        private void CopyFields(string projectNameSrc, IDictionary fieldMapAll, WorkItem workItemSrc, WorkItem workItemDst, string teamArea)
        {
            /* assign relevent fields*/
            if (workItemSrc.ChangedDate < workItemDst.ChangedDate)
                return;

            workItemDst.Fields["OrgID"].Value = workItemSrc.Id;

            foreach (Field field in workItemSrc.Fields)
            {
                if (field.Name.Contains("ID") || field.Name.Contains("State") || field.Name.Contains("Reason"))
                {
                    continue;
                }

                if (workItemDst.Fields.Contains(field.Name) && workItemDst.Fields[field.Name].IsEditable && workItemDst.Fields[field.Name].Value?.ToString() != field.Value?.ToString())
                {
                    workItemDst.Fields[field.Name].Value = field.Value;
                    if (field.Name == "Iteration Path" || field.Name == "Area Path" || field.Name == "Node Name" || field.Name == "Team Project")
                    {
                        try
                        {
                            if (field.Name == "Iteration Path")
                            {
                                teamArea = OldIterations + teamArea;
                            }
                                var areaPathDst = field.Value?.ToString().ToAreaPathDst(projectNameSrc, _projectDst.Name, teamArea);
                            workItemDst.Fields[field.Name].Value = areaPathDst;
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug($"field.Name={field.Name}, field.value={field.Value}, {ex.InnerException}");
                        }
                    }
                }
                //Add values to mapped fields
                else
                {
                    var fieldMap = ListToTable((List<object>) fieldMapAll[workItemSrc.Type.Name]);
                    if (fieldMap.ContainsKey(field.Name))
                    {
                        workItemDst.Fields[(string) fieldMap[field.Name]].Value = field.Value;
                    }
                }
            }
            if (workItemDst.Revisions.Count <= 1)
            {
                workItemDst.Fields["System.ChangedDate"].Value = workItemSrc.Revisions[0].Fields["System.ChangedDate"].Value;
                workItemDst.Fields["System.ChangedBy"].Value = workItemSrc.Revisions[0].Fields["System.ChangedBy"].Value;
            }
        }



        private static Hashtable ListToTable(List<object> map)
        {
            var table = new Hashtable();
            if (map != null)
            {
                foreach (object[] item in map)
                {
                    try
                    {
                        table.Add((string) item[0], (string) item[1]);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error in ListToTable", ex);
                    }
                }
            }
            return table;
        }

        private void ReadItemMap(string sourceProjectName)
        {
            var filaPath = $@"Map\ID_map_{sourceProjectName}_to_{_projectDst.Name}.txt";
            _itemMap = new Dictionary<int, int>();
            if (File.Exists(filaPath))
            {
                var file = new StreamReader(filaPath);
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    if (line.Contains("Source ID|Target ID"))
                    {
                        continue;
                    }
                    var idMap = line.Split('|');
                    if (idMap[0].Trim() != "" && idMap[1].Trim() != "")
                    {
                        _itemMap.Add(Convert.ToInt32(idMap[0].Trim()), Convert.ToInt32(idMap[1].Trim()));
                    }
                }
                file.Close();
            }
        }

        /* Set links between workitems */

        private void CreateLinks(IEnumerable<WorkItem> newWorkItemsSrc)
        {
            Console.WriteLine("Restoring Links between WIT");
            var linkedWorkItemList = new List<int>();
            //GetWorkItemCollection();
            foreach (var workItem in newWorkItemsSrc)
            {
                WorkItem workItemDst = null;

                foreach (WorkItemLink link in workItem.WorkItemLinks)
                {
                    workItemDst = workItemDst ?? _projectDst.Store.GetWorkItem(_itemMap[workItem.Id]);
                    try
                    {
                        var targetIdSrc = link.TargetId;
                        if (_itemMap.ContainsKey(targetIdSrc))
                        {
                            var targetIdDst = _itemMap[targetIdSrc];

                            //if the link is not already created(check if target id is not in list)
                            if (!linkedWorkItemList.Contains(link.TargetId))
                            {
                                try
                                {
                                    var linkTypeEnd = _projectDst.Store.WorkItemLinkTypes.LinkTypeEnds[link.LinkTypeEnd.Name];
                                    workItemDst.Links.Add(new RelatedLink(linkTypeEnd, targetIdDst));

                                    var error = workItemDst.Validate();
                                    if (error.Count == 0)
                                    {
                                        workItemDst.Save();
                                        workItemDst.Close();
                                        workItemDst.Open();
                                    }
                                    else
                                    {
                                        Logger.Info("WorkItem Validation failed at link setup for work item: " + workItem.Id);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.ErrorFormat("Error occured when crearting link for work item: {0} target item: {1}", workItem.Id, link.TargetId);
                                    Logger.Error("Error detail", ex);
                                }
                            }
                        }
                        else
                        {
                            Logger.Info("Link is not created for work item: " + workItem.Id + " - target item: " + link.TargetId + " does not exist");
                        }
                    }
                    catch (Exception)
                    {
                        Logger.Warn("Link is not created for work item: " + workItem.Id + " - target item: " + link.TargetId + " is not in Source TFS or you do not have permission to access");
                    }
                }
                //add the work item to list if the links are processed
                linkedWorkItemList.Add(workItem.Id);
            }
        }

        /* Upload attachments to workitems from local folder */

        private static void UploadAttachments(WorkItem workItemOld, WorkItem workItem)
        {
            var attachmentCollection = workItemOld.Attachments;
            foreach (Attachment att in attachmentCollection)
            {
                var comment = att.Comment;
                var nameWithId = $@"Attachments\{workItemOld.Id}\{att.Id}_{att.Name}";
                try
                {
                    workItem.Attachments.Add(new Attachment(nameWithId, comment));
                }
                catch (Exception ex)
                {
                    Logger.ErrorFormat("Error saving attachment: {0} for workitem: {1}", att.Name, workItemOld.Id);
                    Logger.Error("Error detail: ", ex);
                }
            }
        }

        private void AddStateChangeInfo(WorkItem workItemSrc, WorkItem workItemDst)
        {
            //get the state transition history of the source work item.
            foreach (Revision revisionSrc in workItemSrc.Revisions)
            {
                var orgStateDst = workItemDst.Fields["State"].Value;
                var newStateSrc = revisionSrc.Fields["State"].Value;
                var newStateDst = TeamProjectWorkItemType.GetStateDst(_projectDst, revisionSrc.WorkItem, newStateSrc.ToString());
                try
                {
                    if (!workItemDst.Fields["State"].Value.Equals(newStateDst) && workItemDst.ChangedDate < (DateTime) revisionSrc.Fields["System.ChangedDate"].Value)
                    {
                        workItemDst.Fields["State"].Value = newStateDst;
                        workItemDst.Fields["Changed By"].Value = revisionSrc.Fields["Changed By"].Value;
                        workItemDst.Fields["System.ChangedDate"].Value = revisionSrc.Fields["System.ChangedDate"].Value;
                        workItemDst.Save();
                        workItemDst.Close();
                        workItemDst.Open();
                    }
                }
                catch (Exception)
                {
                    Logger.WarnFormat("Failed to save state for WorkItemDst: {0}  type:'{1}' state from '{2}' to '{3}' => rolling WorkItemDst status to original state '{4}'",
                        workItemDst.Id, workItemDst.Type.Name, orgStateDst, newStateSrc, orgStateDst);
                    //Revert back to the original value.
                    workItemDst.Fields["State"].Value = orgStateDst;
                }
            }
        }

        /* write ID mapping to local file */

        private void WriteMaptoFile(string sourceProjectName)
        {
            Console.WriteLine("Writing Mapping file to disk. File can be used with tfsgit");
            var filaPath = $@"Map\ID_map_{sourceProjectName}_to_{_projectDst.Name}.txt";
            if (!Directory.Exists(@"Map"))
            {
                Directory.CreateDirectory(@"Map");
            }
            else if (File.Exists(filaPath))
            {
                File.WriteAllText(filaPath, string.Empty);
            }

            using (var file = new StreamWriter(filaPath, false))
            {
                file.WriteLine("Source ID|Target ID");
                foreach (var key in _itemMap)
                {
                    var item = key;
                    file.WriteLine(item.Key + "\t | \t" + item.Value);
                }
            }
        }
    }
}