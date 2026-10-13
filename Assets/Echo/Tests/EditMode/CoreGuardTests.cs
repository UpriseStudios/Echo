using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Echo.Core.Sim;
using NUnit.Framework;
using UnityEngine;

namespace Echo.Tests.EditMode
{
    /// <summary>Enforces the determinism rules in tech spec 3 on Echo.Core.</summary>
    public class CoreGuardTests
    {
        static readonly (string pattern, string why)[] Forbidden =
        {
            (@"\bUnityEngine\b", "no engine references"),
            (@"\b(float|double|decimal|Single|Double)\b", "no floating point"),
            (@"System\.Random|\bnew\s+Random\b", "use Xorshift32"),
            (@"\bTime\.", "no Time.*"),
            (@"\bDateTime\b", "no DateTime"),
            (@"\bStopwatch\b", "no wall-clock time"),
        };

        [Test]
        public void CoreAssembly_DoesNotReferenceUnity()
        {
            var refs = typeof(Runner).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.IsFalse(refs.Any(n => n.StartsWith("Unity")), string.Join(", ", refs));
        }

        [Test]
        public void CoreSource_UsesNoForbiddenApis()
        {
            string coreDir = Path.Combine(Application.dataPath, "Echo", "Core");
            var problems = (
                from file in Directory.GetFiles(coreDir, "*.cs", SearchOption.AllDirectories)
                let lines = File.ReadAllLines(file)
                from i in Enumerable.Range(0, lines.Length)
                let code = Regex.Replace(lines[i], @"//.*$", "")
                from rule in Forbidden
                where Regex.IsMatch(code, rule.pattern)
                select $"{Path.GetFileName(file)}:{i + 1} ({rule.why}): {lines[i].Trim()}").ToList();

            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
