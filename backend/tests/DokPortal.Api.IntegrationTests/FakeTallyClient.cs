using DokPortal.Application.FormSubmissions;

namespace DokPortal.Api.IntegrationTests;

/// <summary>API Tally bez sieci: oddaje zgłoszenia ustawione w teście.</summary>
public sealed class FakeTallyClient : ITallyClient
{
    public bool IsConfigured { get; set; } = true;
    public List<TallyEvent> Events { get; } = new();
    public List<string> AskedForms { get; } = new();

    public Task<IReadOnlyList<TallyEvent>> ListSubmissionsAsync(string formId, DateTime sinceUtc, CancellationToken ct)
    {
        lock (AskedForms) AskedForms.Add(formId);
        lock (Events) return Task.FromResult<IReadOnlyList<TallyEvent>>(Events.Where(e => e.FormId == formId && e.SubmittedAtUtc >= sinceUtc).ToList());
    }
}
