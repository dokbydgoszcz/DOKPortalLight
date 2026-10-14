namespace DokPortal.Application.Mailing;

/// <summary>Ustawienia kolejki wysyłki kampanii.</summary>
public sealed record MailingOptions
{
    /// <summary>Ile wiadomości kampanii może wyjść w ciągu 24 godzin (Gmail: ok. 500 na dobę – zostawiamy zapas na przypomnienia i hasła).</summary>
    public int DailyLimit { get; init; } = 400;
    /// <summary>Odstęp między kolejnymi wiadomościami (serwery poczty nie lubią serii).</summary>
    public TimeSpan DelayBetweenMessages { get; init; } = TimeSpan.Zero;
}

/// <summary>Wynik jednego przebiegu kolejki.</summary>
public class MailingQueueResultDto
{
    public required int Sent { get; init; }
    public required int Failed { get; init; }
    /// <summary>Wiadomości, które czekają na kolejny przebieg (np. jutro, po wyczerpaniu limitu).</summary>
    public required int Pending { get; init; }
    public required int DailyLimit { get; init; }
    /// <summary>Wiadomości kampanii wysłane w ostatnich 24 godzinach (łącznie z tym przebiegiem).</summary>
    public required int SentLast24h { get; init; }
}

/// <summary>Uruchamia wysyłkę kolejki w tle (nie czeka na jej koniec).</summary>
public interface IMailingQueue
{
    void Kick();
}

/// <summary>Co widzi osoba po kliknięciu „wypisz się” w stopce.</summary>
public class MailingSubscriptionDto
{
    public required string FirstName { get; init; }
    public required bool OptedOut { get; init; }
}
