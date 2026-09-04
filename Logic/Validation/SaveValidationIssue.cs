namespace MCDSaveEdit.Logic.Validation
{
    public sealed class SaveValidationIssue
    {
        public SaveValidationIssue(SaveValidationSeverity severity, string path, string message)
        {
            Severity = severity;
            Path = path;
            Message = message;
        }

        public SaveValidationSeverity Severity { get; }
        public string Path { get; }
        public string Message { get; }

        public override string ToString()
        {
            return $"[{Severity}] {Path}: {Message}";
        }
    }
}
