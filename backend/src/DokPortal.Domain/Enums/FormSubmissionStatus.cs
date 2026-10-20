namespace DokPortal.Domain.Enums;

/// <summary>Stan zgłoszenia z formularza zewnętrznego (Tally). Wartości zapisane w bazie – nie zmieniać.</summary>
public enum FormSubmissionStatus
{
    /// <summary>Czeka na przejrzenie – nic jeszcze nie zapisano w osobach.</summary>
    Pending = 0,
    /// <summary>Dane przeniesione do osoby (nowej albo istniejącej).</summary>
    Applied = 1,
    /// <summary>Odrzucone (np. dubel, test, spam).</summary>
    Rejected = 2
}
