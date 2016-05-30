using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using log4net;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace MoveTeamProject
{
    public class TeamProjectWorkItemDefinition
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof (TeamProjectWorkItem));
        private readonly Project _projectDst;

        private readonly Dictionary<string, XmlDocument> _dictionary = new Dictionary<string, XmlDocument>();

        public TeamProjectWorkItemDefinition(Project projectDst)
        {
            _projectDst = projectDst;
        }

        public void AddMissingFields(Project projectSrc)
        {
            var workItemTypesSrc = projectSrc.WorkItemTypes;
            foreach (WorkItemType workItemTypeSrc in workItemTypesSrc)
            {
                var workItemTypeDst = TeamProjectWorkItemType.GetWorkItemTypeDst(_projectDst, workItemTypeSrc.Name);
                Console.WriteLine($"Add missing field for {workItemTypeDst.Name}");

                //Save current used fields
                var workItemTypeXmlSrc = workItemTypeSrc.Export(false);
                var workItemTypeXmlDst = workItemTypeDst.Export(false);

                workItemTypeXmlDst = AddNewFields(workItemTypeXmlSrc, workItemTypeXmlDst);
                if (!HasField(workItemTypeXmlDst, "OrgID"))
                {
                    workItemTypeXmlDst = AddCustomField(workItemTypeXmlDst, "<FIELD reportable='dimension' name='OrgID' refname='Medella.OrgID' type='Integer'><HELPTEXT>WorkItemID before migration</HELPTEXT></FIELD>".Replace("'", "\""));
                }

                try
                {
                    WorkItemType.Validate(_projectDst, workItemTypeXmlDst.InnerXml);
                    _projectDst.WorkItemTypes.Import(workItemTypeXmlDst.InnerXml);
                }
                catch (XmlException)
                {
                    Logger.Info("XML import falied for " + workItemTypeSrc.Name);
                }
            }
        }

        public void AddMissingFields(WorkItemStore storeSrc, IEnumerable<string> projectsSrc)
        {
            foreach (var projectSrc in projectsSrc)
            {
                var workItemTypesSrc = storeSrc.Projects[projectSrc].WorkItemTypes;
                foreach (WorkItemType workItemTypeSrc in workItemTypesSrc)
                {
                    Console.WriteLine($"{projectSrc}-{workItemTypeSrc.Name}");
                    var workItemTypeXmlSrc = workItemTypeSrc.Export(false);

                    XmlDocument workItemTypeXmlDst;
                    if (_dictionary.ContainsKey(workItemTypeSrc.Name))
                        workItemTypeXmlDst = _dictionary[workItemTypeSrc.Name];
                    else
                    {
                        var workItemTypeDst = TeamProjectWorkItemType.GetWorkItemTypeDst(_projectDst, workItemTypeSrc.Name);
                        workItemTypeXmlDst = workItemTypeDst.Export(false);
                    }

                    workItemTypeXmlDst = AddNewFields(workItemTypeXmlSrc, workItemTypeXmlDst);
                    if (!HasField(workItemTypeXmlDst, "OrgID"))
                    {
                        workItemTypeXmlDst = AddCustomField(workItemTypeXmlDst, "<FIELD reportable='dimension' name='OrgID' refname='Medella.OrgID' type='Integer'><HELPTEXT>WorkItemID before migration</HELPTEXT></FIELD>".Replace("'", "\""));
                    }

                    if (_dictionary.ContainsKey(workItemTypeSrc.Name))
                    {
                        _dictionary[workItemTypeSrc.Name] = workItemTypeXmlDst;
                    }
                    else
                    {
                        _dictionary.Add(workItemTypeSrc.Name, workItemTypeXmlDst);
                    }
                }
            }
            foreach (var workItemTypeXmlDst in _dictionary)
            {
                try
                {
                    WorkItemType.Validate(_projectDst, workItemTypeXmlDst.Value.InnerXml);
                    _projectDst.WorkItemTypes.Import(workItemTypeXmlDst.Value.InnerXml);
                }
                catch (XmlException)
                {
                    Logger.Info("XML import falied for " + workItemTypeXmlDst.Key);
                }
            }
        }

        private static XmlDocument AddNewFields(XmlDocument workItemTypeXmlSrc, XmlDocument workItemTypeXmlDst)
        {
            var parentNode = workItemTypeXmlDst.GetElementsByTagName("FIELDS")[0];
            var fieldList = GetSrcOnlyFields(workItemTypeXmlSrc, workItemTypeXmlDst);

            if (fieldList.Count > 0)
                Console.WriteLine($"    adding {string.Join(",", fieldList)}");
            //<FIELD name="WorkItemSrc" refname="Microsoft.VSTS.Common.ActivatedDate" type="DateTime" reportable="dimension"><WHENNOTCHANGED field="System.State"><READONLY /></WHENNOTCHANGED></FIELD>
            //<FIELD name='WorkitemIDSrc' refname='WorkitemIDSrc' type='Integer'><HELPTEXT>WorkItemID before migration</HELPTEXT></FIELD>
            //<FIELD name='projectSrc' refname='projectSrc' type='Integer'><HELPTEXT>WorkItem before migration</HELPTEXT></FIELD>
            foreach (var xmlDefSrc in fieldList.Select(fieldName => workItemTypeXmlSrc.SelectNodes($"//FIELD[@name='{fieldName}']")?[0]))
            {
                try
                {
                    var copiedNode = workItemTypeXmlDst.ImportNode(xmlDefSrc, true);
                    parentNode.AppendChild(copiedNode);
                }
                catch (Exception)
                {
                    Logger.ErrorFormat("Error adding new field for parent node : {0}", parentNode.Value);
                }
            }
            return workItemTypeXmlDst;
        }

        private static XmlDocument AddCustomField(XmlDocument workItemTypeXmlDst, string xmlDefSrc)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xmlDefSrc);
            XmlNode xmlNode = doc.DocumentElement;

            var parentNode = workItemTypeXmlDst.GetElementsByTagName("FIELDS")[0];
            var copiedNode = workItemTypeXmlDst.ImportNode(xmlNode, true);
            parentNode.AppendChild(copiedNode);
            return workItemTypeXmlDst;
        }

        private static List<string> GetSrcOnlyFields(XmlDocument workItemTypeXmlSrc, XmlDocument workItemTypeXmlDst)
        {
            return workItemTypeXmlSrc.GetElementsByTagName("FIELD").Cast<XmlNode>()
                .Where(x => x.Attributes?["name"] != null).Select(x => x.Attributes["name"].Value)
                .Where(fieldName => workItemTypeXmlDst.SelectNodes($"//FIELD[@name='{fieldName}']")?.Count == 0).ToList();
        }

        private static bool HasField(XmlDocument workItemTypeXml, string fieldName)
        {
            return workItemTypeXml.GetElementsByTagName("FIELD").Cast<XmlNode>().Any(x => x.Attributes?["name"]?.Value == fieldName);
        }

        public Hashtable MapFields(WorkItemTypeCollection workItemTypesSrc)
        {
            var fieldMap = new Hashtable();
            foreach (WorkItemType workItemTypeSrc in workItemTypesSrc)
            {
                var fieldList = new List<List<string>>();
                var workItemTypeDst = TeamProjectWorkItemType.GetWorkItemTypeDst(_projectDst, workItemTypeSrc.Name);
                var workItemTypeXmlSrc = workItemTypeSrc.Export(false);
                var workItemTypeXmlDst = workItemTypeDst.Export(false);
                fieldList.Add(GetSrcOnlyFields(workItemTypeXmlSrc, workItemTypeXmlDst));
                fieldList.Add(GetSrcOnlyFields(workItemTypeXmlDst, workItemTypeXmlSrc));
                fieldMap.Add(workItemTypeDst.Name, fieldList);
            }
            return fieldMap;
        }
    }
}