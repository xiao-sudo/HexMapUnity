using System;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace HexMap.Editor
{
    /// <summary>
    /// Temporary diagnostic: prints the instanced-draw and sorting API surface Unity actually has.
    /// </summary>
    /// <remarks>
    /// Written to settle whether <c>DrawMeshInstanced</c> can carry a sorting layer or order, which
    /// decides whether render bands can be ordered that way. Delete once the answer is recorded in
    /// the render-order reference.
    /// </remarks>
    internal static class GraphicsSortingProbe
    {
        [UnityEditor.MenuItem("HexMap/Probe/Print Instanced Sorting API")]
        private static void Print()
        {
            var report = new StringBuilder();
            report.AppendLine("=== Graphics instanced-draw overloads ===");
            DumpMethods(report, typeof(Graphics), "DrawMeshInstanced");
            DumpMethods(report, typeof(Graphics), "RenderMeshInstanced");
            DumpMethods(report, typeof(Graphics), "RenderMesh");

            report.AppendLine();
            report.AppendLine("=== RenderParams ===");
            DumpType(report, "UnityEngine.RenderParams");

            report.AppendLine();
            report.AppendLine("=== Renderer sorting members ===");
            DumpType(report, "UnityEngine.Renderer");

            report.AppendLine();
            report.AppendLine("=== SortingLayer ===");
            DumpMembers(report, typeof(SortingLayer));

            Debug.Log(report.ToString());
            Debug.Log("GraphicsSortingProbe wrote " + report.Length + " chars.");
        }

        private static void DumpMethods(StringBuilder report, Type type, string name)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
            var found = false;
            foreach (var method in methods)
            {
                if (method.Name != name)
                {
                    continue;
                }

                found = true;
                var parameters = method.GetParameters();
                var text = new StringBuilder("  " + method.Name + "(");
                for (var index = 0; index < parameters.Length; index++)
                {
                    if (index > 0)
                    {
                        text.Append(", ");
                    }

                    text.Append(parameters[index].ParameterType.Name)
                        .Append(' ')
                        .Append(parameters[index].Name);
                }

                text.Append(')');
                report.AppendLine(text.ToString());
            }

            if (!found)
            {
                report.AppendLine("  (no " + name + " overloads found)");
            }
        }

        private static void DumpType(StringBuilder report, string typeName)
        {
            var type = Type.GetType(typeName + ", UnityEngine.CoreModule");
            if (type == null)
            {
                report.AppendLine("  (type not found: " + typeName + ")");
                return;
            }

            DumpMembers(report, type);
        }

        private static void DumpMembers(StringBuilder report, Type type)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                report.AppendLine("  field " + field.FieldType.Name + " " + field.Name);
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var writable = property.CanWrite ? " {get;set;}" : " {get;}";
                report.AppendLine("  prop  " + property.PropertyType.Name + " " + property.Name + writable);
            }
        }
    }
}
