using System;
using System.Configuration;
using System.Linq;

namespace MoveTeamProject
{
    public static class StringExtenstions
    {
        private static readonly string NameSpace = ConfigurationManager.AppSettings["RemoveNameSpace"];

        public static string RemovePartsFromPath(this string s, string remove)
        {
            if(!string.IsNullOrEmpty(NameSpace))
                s= s.Replace(NameSpace, "");
            return string.IsNullOrEmpty(remove) ? s : s.Replace(remove, "");
        }
        public static string ToNodeIterationPath(this string pathSrc)
        {
            const string value = "\\Iteration";
            return ToNodePath(pathSrc, value);
        }
        public static string ToNodeAreaPath(this string pathSrc)
        {
            const string value = "\\Area";
            return ToNodePath(pathSrc, value);
        }

        private static string ToNodePath(string pathSrc, string value)
        {
            var nodePathSrc = pathSrc+value;
            var index = pathSrc.IndexOf(@"\", 2, StringComparison.Ordinal);
            if (index > 0)
            {
                nodePathSrc = pathSrc.Insert(index, value);
            }
            return nodePathSrc;
        }
        public static string ToApiPath(this string nodePath)
        {
            var apiPath= string.Join("/", nodePath.Split('\\'), 3, nodePath.Split('\\').Length - 3);
            var apiPath2 = string.Join("/", nodePath.Split('\\').Skip(2));
            return apiPath;
        }
        public static string ToAreaPathDst(this string areaPath, string projectNameSrc, string projectNameDst, string teamArea)
        {
            var length = projectNameSrc.Length;
            var itPathNew = projectNameDst + teamArea + areaPath?.Substring(length);
            return itPathNew;
        }

    }
}