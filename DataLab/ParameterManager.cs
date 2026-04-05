using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLab
{
    public static class ParameterManager
    {
        public static IList<Parameter> GetElementParameter(this Element ele)
        {
            if (ele == null) return new List<Parameter>();

            var list = new List<Parameter>();

            // Get all instance parameters
            var instanceParams = ele.Parameters;

            // Get all type parameters
            var typeParams = ele.GetTypeId() != ElementId.InvalidElementId
                ? ele.Document.GetElement(ele.GetTypeId()).Parameters
                : null;

            if (typeParams != null)
            {
                foreach (Parameter param in typeParams)
                {
                    list.Add(param);
                }
            }

            if (instanceParams != null)
            {
                foreach (Parameter param in instanceParams)
                {
                    list.Add(param);
                }
            }

            // Deduplicate and sort
            var seen = new HashSet<string>();
            var deduped = new List<Parameter>();

            foreach (var param in list.OrderBy(x => x.Definition.Name))
            {
                string key;

                if (param.IsShared)
                {
                    // Shared params: use GUID as true identity
                    key = param.GUID.ToString();
                }
                else
                {
                    // Built-in/project params: deduplicate by name + group
                    key = $"{param.Definition.Name}|{param.Definition.GetGroupTypeId()}";
                }

                if (seen.Add(key))
                {
                    deduped.Add(param);
                }
            }

            return deduped;
        }
    }
}