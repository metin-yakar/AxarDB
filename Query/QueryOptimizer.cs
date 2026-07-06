using Jint;
using Jint.Native;
using System.Text.RegularExpressions;

namespace AxarDB.Query
{
    public class QueryOptimizer
    {
        public struct AnalysisResult { public string Prop; public object Val; public string Op; }

        public static (string? prop, object? val, string op)? AnalyzePredicate(JsValue predicate)
        {
            if (predicate == null || !predicate.IsObject())
                return null;

            // predicate.ToString() returns code like "x => x.name == 'alice'"
            string code = predicate.ToString();

            // Match simple binary expressions: x.prop == "val", x.prop > 10
            // Regex for arrow functions or simple returns
            // Pattern: member access operator value
            // Allow: x.name, x['name'], name (if scoped)
            
            // Example: x => x.name == "Test"
            // Regex: \w+\.(\w+)\s*(==|===)\s*(?:""|')(.+?)(?:""|')
            
            // This is a naive heuristic but works for simple queries requested by prompt
            var match = Regex.Match(code, @"(?:x|item|doc)\.(\w+)\s*(==|===)\s*(?:""|')(.+?)(?:""|')");
            if (match.Success)
            {
                return (match.Groups[1].Value, match.Groups[3].Value, "==");
            }
            
            // Numbers
            var matchNum = Regex.Match(code, @"(?:x|item|doc)\.(\w+)\s*(==|===|>|<|>=|<=)\s*(\d+(\.\d+)?)");
            if (matchNum.Success)
            {
                double val = double.Parse(matchNum.Groups[3].Value);
                return (matchNum.Groups[1].Value, val, matchNum.Groups[2].Value);
            }

            return null;
        }

        public static HashSet<string> ExtractAccessedProperties(JsValue predicate)
        {
            var props = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            props.Add("_id"); // Always include ID

            if (predicate == null || !predicate.IsObject())
                return props;

            string code = predicate.ToString();
            
            // Match dot notation: x.prop, item.prop, doc.prop
            var matches = Regex.Matches(code, @"\b(?:x|item|doc)\.(\w+)\b");
            foreach (Match match in matches)
            {
                props.Add(match.Groups[1].Value);
            }

            // Match bracket notation: x['prop'], x["prop"]
            var bracketMatches = Regex.Matches(code, @"\b(?:x|item|doc)\[[""'](\w+)[""']\]");
            foreach (Match match in bracketMatches)
            {
                props.Add(match.Groups[1].Value);
            }

            return props;
        }
    }
}
