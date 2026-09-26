namespace AI.TeachingAssistant.Application.Common;

/// <summary>Expected input failure; carries a user-facing Persian message. Never leaks internals.</summary>
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
