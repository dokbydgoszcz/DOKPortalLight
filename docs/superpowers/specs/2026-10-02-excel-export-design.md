# Eksport list do Excela — projekt

Data: 2026-10-02. Faza 3 (raportowanie), część 2. Dashboard został zrobiony wcześniej (commit `b56fbd7`).

## Cel

Uprawnieni użytkownicy mogą pobrać z każdego z ośmiu ekranów list plik `.xlsx` z wszystkimi nieusuniętymi rekordami tej listy.

## Decyzje (zatwierdzone)

- Format: Excel `.xlsx` (biblioteka `ClosedXML`, licencja MIT), generowany po stronie serwera.
- Zakres: Osoby, Podopieczni DOK, Kandydaci SKŚP, Misje, Formatorzy, Superwizje, Spotkania, Parafie.
- Eksport obejmuje wszystkie nieusunięte rekordy listy, bez filtrów z ekranu (YAGNI). Globalny filtr soft-delete robi to automatycznie.
- Brak migracji bazy.

## Backend

- `Application/Export/IExportService.cs` — osiem metod `Task<byte[]> Export<Lista>Async(CancellationToken ct)`.
- `Infrastructure/Services/ExportService.cs` — jeden prywatny helper `BuildWorkbook(sheetName, headers, rows)`: pogrubione nagłówki, zamrożony pierwszy wiersz, automatyczna szerokość kolumn. Każda metoda mapuje encje na wiersze tekstowe/liczbowe. Zapytania z `AsNoTracking`, sortowane stabilnie (nazwisko/data).
- `Api/Controllers/ExportController.cs`, trasa `api/export`, `[Authorize]`; endpointy `GET people`, `dok-cases`, `candidates`, `missions`, `formators`, `supervisions`, `meetings`, `parishes` zwracają `File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "<nazwa>.xlsx")`.
- Każdy eksport zapisuje wpis w `IAuditLogService.LogAsync(userId, email, "ExportData", "<lista>", AuditResult.Allowed, ct)` (wzorzec jak w `PastoralNotesController`: id i e-mail z claimów `sub` / `email`).
- Rejestracja `IExportService` w `Program.cs` (`AddScoped`).

### Uprawnienia (każdy endpoint ma własny `[Authorize(Roles=...)]`)

| Lista | Role |
|---|---|
| people | Administrator, DyrektorSKSP, DyrektorDOK |
| dok-cases | Administrator, DyrektorDOK |
| candidates | Administrator, DyrektorSKSP |
| missions | Administrator, DyrektorSKSP |
| formators | Administrator, DyrektorSKSP |
| supervisions | Administrator, DyrektorDOK, DyrektorSKSP, Superwizor |
| meetings | Administrator, DyrektorDOK |
| parishes | Administrator |

Role są takie same jak do edycji danej listy, z jednym wyjątkiem: `meetings` nie obejmuje `KatechistaProwadzacy`, bo zwykły katechista nie eksportuje danych hurtowo.

### Kolumny (nagłówki po polsku)

- **Osoby:** Imię, Nazwisko, E-mail, Telefon, Data urodzenia, Parafia, Imieniny (DD.MM). Pole `Notes` pomijamy — to wolny tekst, który może zawierać wrażliwe uwagi.
- **Podopieczni DOK:** Osoba, Ścieżka, Etap, Katechista, Opiekun (mentor), Data ostatniego spotkania, Data zakończenia. Ścieżki i etapy po polsku, etykiety takie jak w formularzu sprawy DOK (Zgłoszenie, Formacja, Sakrament, Absolwent).
- **Kandydaci SKŚP:** Osoba, Rok, Frekwencja (%), Opinie zebrane, Opinie wymagane, Rekolekcje (Tak/Nie).
- **Misje:** Katechista, Miejsce posługi, Data od, Data do, Data udzielenia, Miejsce udzielenia, Grupa superwizyjna.
- **Formatorzy:** Osoba, Funkcja.
- **Superwizje:** Instytucja, Grupa, Data, Obecnych, Oczekiwanych, Temat, Wnioski.
- **Spotkania:** Data, Podopieczny (lub etykieta grupy), Obecność (Tak/Nie/—), Uwagi.
- **Parafie:** Nazwa, Miejscowość.

Daty w formacie `yyyy-MM-dd`; puste wartości jako pusta komórka.

## Frontend

- `shared/export/export.service.ts` — `download(path, fileName)`: `GET` z `responseType: 'blob'`, zapis pliku przez tymczasowy `<a download>`; błąd → toast przez istniejący serwis powiadomień.
- `shared/export/export-button.component.ts` — przycisk „Eksportuj do Excela" (`path`, `fileName`; stan „trwa pobieranie", blokada na czas żądania). Widoczny tylko dla ról z tabeli wyżej (`AuthService.hasAnyRole`) — parametr `roles`.
- Przycisk w nagłówku każdej z ośmiu list.
- Wpis w „Co nowego" na pulpicie.

## Testy

- xUnit `ExportServiceTests` (InMemory): po jednym teście na listę — otwarcie wyniku przez `XLWorkbook`, asercje nagłówków i wierszy; test pominięcia rekordów usuniętych (dla osób) oraz tłumaczenia etapu/ścieżki (dla spraw DOK).
- Integracyjny `ExportControllerTests`: 200 + content-type xlsx dla roli uprawnionej, 403 dla roli nieuprawnionej (katechista), 401 bez logowania; wpis w audycie po eksporcie.
- Vitest: `ExportService` (żądanie blob + wywołanie zapisu), `ExportButtonComponent` (widoczność wg roli, wywołanie pobrania).

## Poza zakresem

Filtry z ekranu w eksporcie, import z Excela, eksport budżetu i potrzeb parafialnych, eksport CSV, harmonogram/e-mailowa wysyłka raportów.
