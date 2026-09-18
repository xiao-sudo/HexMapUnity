#if UNITY_EDITOR
using System.Collections.Generic;

namespace HexMap.Gvg.Authoring
{
    public enum GvgMapAuthoringValidationSeverity
    {
        Error = 0
    }

    public readonly struct GvgMapAuthoringValidationIssue
    {
        public GvgMapAuthoringValidationIssue(GvgMapAuthoringValidationSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }

        public GvgMapAuthoringValidationSeverity Severity { get; }
        public string Message { get; }
    }

    public sealed class GvgMapAuthoringValidationResult
    {
        private readonly List<GvgMapAuthoringValidationIssue> m_Issues;

        public GvgMapAuthoringValidationResult(List<GvgMapAuthoringValidationIssue> issues)
        {
            m_Issues = issues ?? new List<GvgMapAuthoringValidationIssue>();
        }

        public IReadOnlyList<GvgMapAuthoringValidationIssue> Issues
        {
            get { return m_Issues; }
        }

        public bool IsValid
        {
            get { return m_Issues.Count == 0; }
        }
    }
}
#endif