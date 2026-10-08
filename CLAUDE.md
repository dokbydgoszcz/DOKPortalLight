# DOK Portal — instrukcje dla Claude

Projekt: CRM Diecezji Bydgoskiej (SKŚP + DOK). Backend ASP.NET Core 8 (`backend/`), frontend Angular 22 (`frontend/`).

**Zanim zaczniesz pracę, przeczytaj `docs/HANDOFF.md`** — stan projektu, zasady pracy, pułapki środowiska i lista otwartych spraw. Plan napraw z przeglądu: `docs/przeglad-procesu-2026-10-08.md`. Runbook operacyjny: `docs/operations.md`.

Zasady (skrót, pełne w HANDOFF):
- Rozmowa po polsku.
- Commity prosto na `master`; push dopiero po wyraźnym OK użytkownika.
- Nigdy nie obsługuj sekretów; polecenia `az` zmieniające Azure — najpierw zgoda.
- Nie commituj `backend/src/DokPortal.Api/appsettings.Development.json` ani `.claude/`; stage’uj tylko konkretne pliki.
- TDD; przy czerwonych testach nie commituj; wpis „Co nowego” na pulpicie przy każdej funkcji.
