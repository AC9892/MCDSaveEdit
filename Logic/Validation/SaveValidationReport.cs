using System.Collections.Generic;
using System.Linq;

namespace MCDSaveEdit.Logic.Validation
{
    public sealed class SaveValidationReport
    {
        public SaveValidationReport(IEnumerable<SaveValidationIssue> issues)
        {
            Issues = issues.ToArray();
        }

        public IReadOnlyList<SaveValidationIssue> Issues { get; }
        public bool HasErrors => Issues.Any(issue => issue.Severity == SaveValidationSeverity.Error);
        public bool HasWarnings => Issues.Any(issue => issue.Severity == SaveValidationSeverity.Warning);
        public bool HasMessages => Issues.Count > 0;

        public string ToCopyableText()
        {
            if (!HasMessages) return "No validation issues were found.";
            return string.Join(System.Environment.NewLine, Issues.Select(issue => issue.ToString()));
        }
    }
}
