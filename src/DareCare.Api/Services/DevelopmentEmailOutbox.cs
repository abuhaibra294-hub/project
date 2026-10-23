namespace DareCare.Api.Services;

// In-memory test inbox only. Nothing is persisted to disk or sent to real recipients.
public sealed class DevelopmentEmailOutbox
{
    private readonly object _gate = new();
    private readonly List<DevelopmentMail> _messages = [];

    public void Add(string to, string subject, string body)
    {
        lock (_gate)
        {
            _messages.Add(new DevelopmentMail(to, subject, body, DateTime.UtcNow));
            if (_messages.Count > 40) _messages.RemoveRange(0, _messages.Count - 40);
        }
    }

    public IReadOnlyList<DevelopmentMail> Snapshot()
    {
        lock (_gate) return _messages.OrderByDescending(m => m.SentAtUtc).ToList();
    }
}

public sealed record DevelopmentMail(string To, string Subject, string Body, DateTime SentAtUtc);
