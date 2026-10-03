# Faza 5 — jakość testów — plan implementacji

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Podnieść pokrycie i jakość testów (frontend ≈80% linii / ≈70% funkcji, backend ≈88% linii) i dodać do CI pomiar pokrycia z progami dla frontendu.

**Architecture:** Pomiar `@vitest/coverage-v8` przez builder `@angular/build:unit-test` (progi w `angular.json`); wspólna pomoc `src/app/testing/test-helpers.ts`; testy zachowania komponentów przez `TestBed` + `HttpTestingController` z mockiem `AuthService`; testy luk backendu na istniejących wzorcach (InMemory dla serwisów, `IntegrationTestBase` dla kontrolerów).

**Tech Stack:** Angular 22, Vitest 4.1.11, `@vitest/coverage-v8@4.1.11`; ASP.NET Core 8, xUnit, coverlet.collector.

**Spec:** `docs/superpowers/specs/2026-10-03-test-quality-design.md`

## Global Constraints

- Frontend: wspólna pomoc w `frontend/src/app/testing/test-helpers.ts` (wyłączona z pokrycia); testy zachowania sprawdzają **żądanie HTTP** (metoda, URL, ciało) i **widoczny efekt** (tekst, stan przycisku, toast), nie szczegóły implementacji.
- Przy każdym komponencie przeczytać jego `.ts` i `.html` przed pisaniem testów i pokryć **wszystkie** wymienione w zadaniu zachowania; dopasować selektory do istniejącego znacznika (nie zmieniać kodu produkcyjnego tylko dla testów — wyjątek: błąd znaleziony przez test; wtedy zgłosić go osobno i naprawić w osobnym commicie z testem).
- Test, który od razu przechodzi, jest dopuszczalny tylko dla kodu już istniejącego (to testy charakteryzujące); każdy nowy test musi być sprawdzony „na czerwono” przez chwilowe zepsucie asercji lub wskazanie realnej luki z raportu pokrycia (kontrola: pokrycie pliku rośnie).
- Progi pokrycia ustawiać na końcu (Task 7) 1–2 p.p. poniżej osiągniętego poziomu.
- Backend: nie zmieniać kodu produkcyjnego poza poprawkami błędów wykrytych testami.
- **Commity lokalne na `master`, BEZ `git push`** (push tylko po zgodzie użytkownika). Nie commitować `backend/src/DokPortal.Api/appsettings.Development.json` ani `.claude/` (stage’ować tylko wskazane pliki); `frontend/coverage/` nie commitować (dodać do `.gitignore`, jeśli brak).
- Stopka commita: `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- Komendy frontendu z `C:\eu02_install\DOKPortalLight\frontend`, backendu z `...\backend`.

---

### Task 1: Narzędzia pokrycia i wspólna pomoc testowa

**Files:**
- Modify: `frontend/package.json`, `frontend/package-lock.json`, `frontend/angular.json`, `.gitignore` (jeśli brak wpisu `coverage`)
- Create: `frontend/src/app/testing/test-helpers.ts`
- Test: `frontend/src/app/testing/test-helpers.spec.ts`

**Interfaces:**
- Produces (`test-helpers.ts`):
  - `api(path: string): string` — `${environment.apiBaseUrl}${path}`.
  - `provideFakeAuth(granted?: string[]): Provider` — mock `AuthService` (`hasPermission`, `hasAnyPermission`, `roles()`, `permissions()`, `token`, `logout`); `granted = ['*']` oznacza wszystkie uprawnienia.
  - `setup<T>(component: Type<T>, options?: { granted?: string[]; providers?: Provider[] }): { fixture: ComponentFixture<T>; http: HttpTestingController; el: HTMLElement }`.
  - `paged<T>(items: T[], page = 1, pageSize = 20): { items: T[]; totalCount: number; page: number; pageSize: number }`.
  - `flushAll(http: HttpTestingController, url: string, body: unknown): number` — odpowiada na wszystkie oczekujące żądania o tym URL (porównanie po `request.url`), zwraca ich liczbę.
  - `click(el: HTMLElement, selector: string): void`, `clickByText(el: HTMLElement, text: string, selector?: string): void` (domyślnie `'button, span, a, label'`, dopasowanie po `trim()` tekstu zawierającego `text`), `setInput(el: HTMLElement, selector: string, value: string): void` (ustawia `value` i wysyła `input`), `setSelect(el: HTMLElement, selector: string, value: string): void` (ustawia `value` i wysyła `change`), `textOf(el: HTMLElement): string` (spłaszczone spacje).

- [ ] **Step 1: Zainstaluj pakiet pokrycia**

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npm install -D @vitest/coverage-v8@4.1.11 2>&1 | tail -3
git diff --stat package.json package-lock.json
```
Expected: `package.json` dostaje `"@vitest/coverage-v8": "^4.1.11"` w `devDependencies`.

- [ ] **Step 2: Konfiguracja buildera** — w `frontend/angular.json` zastąp blok `"test": { "builder": "@angular/build:unit-test" }` blokiem:

```json
        "test": {
          "builder": "@angular/build:unit-test",
          "options": {
            "coverageExclude": [
              "**/*.spec.ts",
              "**/*.model.ts",
              "src/main.ts",
              "src/app/app.config.ts",
              "src/environments/**",
              "src/app/testing/**"
            ],
            "coverageReporters": ["text-summary", "html"]
          }
        }
```
(zachowaj wcięcia i przecinki zgodnie z sąsiednimi blokami; jeśli `app.config.ts` ma inną nazwę, sprawdź `ls frontend/src/app` i popraw ścieżkę). Dopisz do `.gitignore` w korzeniu repo linię `frontend/coverage/`, jeśli jej nie ma.

- [ ] **Step 3: Test pomocy (czerwony)** — `test-helpers.spec.ts`:

```ts
import { Component } from '@angular/core';
import { describe, it, expect } from 'vitest';
import { FormsModule } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { api, click, clickByText, flushAll, paged, provideFakeAuth, setInput, setSelect, setup, textOf } from './test-helpers';
import { AuthService } from '../core/auth/auth.service';

@Component({
  standalone: true,
  imports: [FormsModule],
  template: `
    <button id="a" (click)="clicks = clicks + 1">Zapisz zmiany</button>
    <span class="x" (click)="clicks = clicks + 10">Usuń</span>
    <input id="name" [(ngModel)]="name" />
    <select id="kind" [(ngModel)]="kind"><option value="a">A</option><option value="b">B</option></select>
    <p>{{ name }}   {{ kind }}</p>
  `
})
class HostComponent {
  clicks = 0;
  name = '';
  kind = 'a';
}

describe('test helpers', () => {
  it('builds api urls and paged payloads', () => {
    expect(api('/api/people')).toContain('/api/people');
    expect(paged([1, 2, 3])).toEqual({ items: [1, 2, 3], totalCount: 3, page: 1, pageSize: 20 });
  });

  it('provides a fake auth that grants everything or only the listed permissions', () => {
    TestBed.configureTestingModule({ providers: [provideFakeAuth(['People.Manage'])] });
    const auth = TestBed.inject(AuthService);

    expect(auth.hasPermission('People.Manage')).toBe(true);
    expect(auth.hasPermission('People.Export')).toBe(false);
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [provideFakeAuth()] });
    expect(TestBed.inject(AuthService).hasPermission('Anything.Goes')).toBe(true);
  });

  it('drives a component through click, input and select helpers', () => {
    const { fixture, el } = setup(HostComponent);
    fixture.detectChanges();

    click(el, '#a');
    clickByText(el, 'Usuń');
    setInput(el, '#name', 'Ola');
    setSelect(el, '#kind', 'b');
    fixture.detectChanges();

    expect(fixture.componentInstance.clicks).toBe(11);
    expect(textOf(el)).toContain('Ola b');
  });

  it('flushes every pending request for a url', () => {
    const { http } = setup(HostComponent);
    TestBed.inject(AuthService);
    const client = TestBed.inject(HttpTestingController);
    client.verify();
    void http;
  });
});
```
Uwaga: czwarty test jest szkieletem — zastąp go (przed uruchomieniem) właściwym:

```ts
  it('flushes every pending request for a url', async () => {
    const { http } = setup(HostComponent);
    const client = TestBed.inject(HttpClient);
    const results: unknown[] = [];
    client.get(api('/api/x')).subscribe(r => results.push(r));
    client.get(api('/api/x')).subscribe(r => results.push(r));

    const flushed = flushAll(http, api('/api/x'), { ok: true });

    expect(flushed).toBe(2);
    expect(results).toEqual([{ ok: true }, { ok: true }]);
  });
```
z importami `import { HttpClient } from '@angular/common/http';` i `import { HttpTestingController } from '@angular/common/http/testing';` (usuń użycia `HttpTestingController` ze szkieletu).

- [ ] **Step 4: Uruchom — czerwone** (`Could not resolve "./test-helpers"`).

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | grep -E "ERROR|Could not resolve|Test Files" | head -3
```

- [ ] **Step 5: Implementacja** — `test-helpers.ts`:

```ts
import { Provider, Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { vi } from 'vitest';
import { AuthService } from '../core/auth/auth.service';
import { environment } from '../../environments/environment';

export const api = (path: string): string => `${environment.apiBaseUrl}${path}`;

export function provideFakeAuth(granted: string[] = ['*']): Provider {
  const allowed = (permission: string) => granted.includes('*') || granted.includes(permission);
  return {
    provide: AuthService,
    useValue: {
      hasPermission: allowed,
      hasAnyPermission: (permissions: string[]) => permissions.some(allowed),
      roles: () => [],
      permissions: () => granted,
      token: null,
      logout: vi.fn()
    }
  };
}

export function setup<T>(
  component: Type<T>,
  options: { granted?: string[]; providers?: Provider[] } = {}
): { fixture: ComponentFixture<T>; http: HttpTestingController; el: HTMLElement } {
  TestBed.configureTestingModule({
    imports: [component],
    providers: [provideHttpClient(), provideHttpClientTesting(), provideFakeAuth(options.granted), ...(options.providers ?? [])]
  });
  const fixture = TestBed.createComponent(component);
  const http = TestBed.inject(HttpTestingController);
  return { fixture, http, el: fixture.nativeElement as HTMLElement };
}

export function paged<T>(items: T[], page = 1, pageSize = 20) {
  return { items, totalCount: items.length, page, pageSize };
}

export function flushAll(http: HttpTestingController, url: string, body: unknown): number {
  const requests = http.match(r => r.url === url);
  requests.forEach(r => r.flush(body));
  return requests.length;
}

export function click(el: HTMLElement, selector: string): void {
  const target = el.querySelector<HTMLElement>(selector);
  if (!target) throw new Error(`Brak elementu: ${selector}`);
  target.click();
}

export function clickByText(el: HTMLElement, text: string, selector = 'button, span, a, label'): void {
  const target = Array.from(el.querySelectorAll<HTMLElement>(selector)).find(e => (e.textContent ?? '').trim().includes(text));
  if (!target) throw new Error(`Brak elementu z tekstem „${text}” (${selector})`);
  target.click();
}

export function setInput(el: HTMLElement, selector: string, value: string): void {
  const input = el.querySelector<HTMLInputElement | HTMLTextAreaElement>(selector);
  if (!input) throw new Error(`Brak pola: ${selector}`);
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

export function setSelect(el: HTMLElement, selector: string, value: string): void {
  const select = el.querySelector<HTMLSelectElement>(selector);
  if (!select) throw new Error(`Brak listy: ${selector}`);
  select.value = value;
  select.dispatchEvent(new Event('change'));
}

export function textOf(el: HTMLElement): string {
  return (el.textContent ?? '').replace(/\s+/g, ' ').trim();
}
```

- [ ] **Step 6: Uruchom — zielone, plus pokrycie**

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false --coverage 2>&1 | grep -E "ERROR|FAIL|Test Files|Tests |Statements|Lines|Functions"
```
Expected: wszystkie testy zielone, podsumowanie pokrycia wypisane; `test-helpers.ts` nie występuje w raporcie (wyłączony).

- [ ] **Step 7: Commit (bez push)**

```bash
cd C:/eu02_install/DOKPortalLight && git add frontend/package.json frontend/package-lock.json frontend/angular.json frontend/src/app/testing .gitignore
git commit -m "$(cat <<'EOF'
Testy frontendu: pomiar pokrycia (coverage-v8) i wspólna pomoc testowa

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Ekran użytkowników i logowanie

**Files:**
- Test: `frontend/src/app/features/admin-users/users-list.component.spec.ts` (rozszerzyć), `frontend/src/app/features/login/login.component.spec.ts` (rozszerzyć)

**Interfaces:** Consumes: `setup`, `api`, `flushAll`, `click`, `clickByText`, `setInput`, `textOf` (Task 1); `UsersService` (`list`, `listRoles`, `create`, `assignRoles`, `resetPassword`), `PeopleService`, `ToastService`.

Zachowania do pokrycia w `users-list` (po przeczytaniu `users-list.component.{ts,html}`):
- [ ] tworzenie konta: walidacja pustego e-maila/hasła/niezgodnych haseł → komunikat `createError`, brak `POST /api/users`; poprawne dane z wybraną osobą → `POST /api/users` z `{ email, password, roles, personId }`, po sukcesie toast „Konto użytkownika utworzone.” i ponowne wczytanie listy; błąd API → komunikat błędu;
- [ ] tworzenie konta z nową osobą (`addingNewPerson`): brak imienia/nazwiska → komunikat; poprawne → najpierw `POST /api/people`, potem `POST /api/users` z `personId` nowej osoby;
- [ ] wyszukiwanie osoby (debounce `onPersonQueryChange`): po wpisaniu frazy `GET /api/people?query=...`, wyniki widoczne, wybór osoby ustawia `selectedPerson`, „Zmień” czyści wybór (użyj `vi.useFakeTimers()` dla debounce);
- [ ] przełączanie roli checkboxem → `PUT /api/users/{id}/roles` z pełną listą ról użytkownika (dodanie i odebranie), po sukcesie ponowne wczytanie listy; błąd → toast „Nie udało się zaktualizować ról.”;
- [ ] reset hasła: start (`startResetPassword`) pokazuje pola, niezgodne hasła → `resetError`, zgodne → `PUT /api/users/{id}/reset-password` z `{ newPassword }`, anulowanie chowa pola;
- [ ] błąd wczytania listy użytkowników i listy ról → toasty.

Zachowania do pokrycia w `login` (po przeczytaniu `login.component.{ts,html}`, istniejący mock `AuthService` zostaje):
- [ ] puste pola → brak wywołania `login`, widoczny komunikat walidacji (jeśli komponent go ma);
- [ ] poprawne dane → `login(email, password)` wywołane z wpisanymi wartościami, nawigacja po sukcesie;
- [ ] błąd 401 → widoczny komunikat o nieprawidłowym logowaniu, przycisk znów aktywny;
- [ ] stan „budzenia serwera” (spinner) podczas oczekiwania, jeśli komponent go ma (sprawdzić w kodzie), przełączanie widoczności hasła (`togglePasswordVisibility`), komunikat po wylogowaniu z bezczynności (`reason: 'idle'` z `history.state`, jeśli obsługiwany).

- [ ] **Step 1:** Przeczytaj oba komponenty i ich spec-i; **Step 2:** napisz testy dla każdego punktu wyżej (jedna `it` na punkt, nazwy opisowe po angielsku jak w istniejących spec-ach); **Step 3:** uruchom `npx ng test --watch=false --coverage` i sprawdź w raporcie, że `users-list.component.ts` ≥ 80% linii, `login.component.ts` ≥ 80% linii; **Step 4:** commit:

```bash
cd C:/eu02_install/DOKPortalLight && git add frontend/src/app/features/admin-users frontend/src/app/features/login
git commit -m "$(cat <<'EOF'
Testy frontendu: zachowanie ekranów użytkowników i logowania

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Sprawy DOK (lista, formularz, notatki, dokumenty) i Osoby

**Files:**
- Test: `frontend/src/app/features/dok-cases/dok-cases-list.component.spec.ts`, `frontend/src/app/features/dok-cases/dok-case-form.component.spec.ts` (nowy), `frontend/src/app/features/people/people-list.component.spec.ts`, `frontend/src/app/features/people/person-form.component.spec.ts` (nowy)

**Interfaces:** Consumes: pomoc z Task 1.

Zachowania do pokrycia:
- [ ] `dok-cases-list`: dodanie sprawy (`POST /api/dok-cases` z `{ personId, path, stage, catechistPersonId, mentorPersonId? }`, toast, ponowne wczytanie); edycja istniejącej (`PUT /api/dok-cases/{id}`); usunięcie z `confirm` (`DELETE`; po anulowaniu brak żądania); paginacja (`onPageChange` → żądanie z `page`); liczniki ścieżek (już pokryte — nie powielać);
- [ ] `dok-cases-list` notatki: `openNotes` → `GET /api/dok-cases/{id}/notes`, lista notatek widoczna, pusta lista → „Brak notatek.”, `addNote` z pustą treścią → brak żądania, z treścią → `POST .../notes` z `{ content }`, po sukcesie pole czyszczone i lista odświeżona, błąd → toast, `closeNotes`;
- [ ] `dok-cases-list` dokumenty: `openDocuments` → `GET .../documents`, `addDocument` (pusta nazwa → brak żądania; z nazwą → `POST` z `{ name }`), wybór pliku (`onFileSelected`) → `POST .../documents/{id}/upload` jako `FormData`, `downloadDocument` wywołuje pobranie bloba (`URL.createObjectURL` zamockowany jak w `export.service.spec.ts`), `formatFileSize` dla B/KB/MB (test jednostkowy metody), `closeDocuments`;
- [ ] `dok-case-form`: przycisk „Zapisz” zablokowany bez osoby i katechisty, aktywny po wyborze obu (regresja błędu select/ngModel), `save` emituje wartość formularza z wybranymi `path`/`stage`, `cancel` emituje, formularz niewidoczny gdy `open=false`;
- [ ] `people-list`: szukanie (`onSearch` → żądanie z `query`, strona 1), paginacja, dodanie osoby (`POST /api/people` z imieniem, nazwiskiem, e-mailem, telefonem, parafią, datą urodzenia, imieninami jeśli formularz je ma), edycja (`PUT`), usunięcie z `confirm`;
- [ ] `person-form`: walidacja wymaganych pól (imię i nazwisko) blokuje zapis lub pokazuje komunikat — zgodnie z faktycznym kodem, `save`/`cancel` emitują, wartość początkowa przy edycji jest wypełniona.

- [ ] **Step 1:** Przeczytaj komponenty i istniejące spec-i; **Step 2:** napisz testy; **Step 3:** `npx ng test --watch=false --coverage`, sprawdź `dok-cases-list.component.ts`, `dok-case-form.component.*`, `people-list.component.ts`, `person-form.component.*` ≥ 80% linii; **Step 4:** commit `Testy frontendu: zachowanie spraw DOK i osób`.

---

### Task 4: Kandydaci, Misje, Formatorzy, Spotkania, Superwizje

**Files:**
- Test: spec-i w `features/candidates/` (`candidates-list`, `candidate-form` nowy), `features/missions/` (`missions-list`, `mission-form` nowy), `features/formators/formators-list`, `features/meetings/meetings-list`, `features/supervisions/supervisions-list` (`.component.spec.ts`)

**Interfaces:** Consumes: pomoc z Task 1.

Zachowania do pokrycia (dla każdego ekranu; dopasować do faktycznych pól komponentu):
- [ ] dodanie rekordu → `POST` z poprawnym ciałem, toast sukcesu, lista odświeżona; błąd API → toast błędu;
- [ ] edycja (tam, gdzie ekran ją ma: Formatorzy, Spotkania, Superwizje) → `PUT /api/<zasób>/{id}` z zaktualizowanymi polami, formularz wypełniony wartościami edytowanego rekordu (`openEditForm`);
- [ ] usunięcie z `confirm` → `DELETE`, po anulowaniu brak żądania;
- [ ] przycisk „Zapisz” w formularzu zablokowany do wyboru wymaganej osoby (Kandydaci, Misje, Formatorzy — regresja błędu select/ngModel);
- [ ] Kandydaci/Misje: paginacja (`onPageChange`), liczniki lat (`yearOneCount` itd.) obliczone z listy;
- [ ] Spotkania: wybór sprawy DOK w formularzu (`onDokCaseChange`): pusty wybór → `dokCaseId` niezdefiniowane (spotkanie grupowe), wybór sprawy → `dokCaseId` w ciele `POST`;
- [ ] anulowanie formularza zamyka go i czyści wartości.

- [ ] **Step 1:** Przeczytaj komponenty, formularze i istniejące spec-i; **Step 2:** napisz testy; **Step 3:** `npx ng test --watch=false --coverage`, sprawdź, że każdy z tych komponentów ≥ 80% linii; **Step 4:** commit `Testy frontendu: zachowanie Kandydatów, Misji, Formatorów, Spotkań i Superwizji`.

---

### Task 5: Giełda parafii, budżety, mailing, dokumenty (+ idle timeout)

**Files:**
- Test: spec-i w `features/parish-board/` (`parish-board`, `parishes-list` nowy), `features/budget/budget`, `features/budget-dok/budget-dok`, `features/mailing/mailing`, `features/documents/documents`, `core/auth/idle-timeout.service.spec.ts` (nowy)

**Interfaces:** Consumes: pomoc z Task 1; `IdleTimeoutService` (`start`, `stop`; limit 20 min, zdarzenia aktywności; po upływie `AuthService.logout('idle')`).

Zachowania do pokrycia:
- [ ] `parish-board`: dodanie zapotrzebowania (`POST /api/parish-needs`), skierowanie katechisty (`openAssignForm` → `confirmAssign` → `PUT /api/parish-needs/{id}/assign` z `{ assignedPersonId }`), usunięcie z `confirm`, błędy → toasty;
- [ ] `parishes-list`: dodanie parafii (`POST /api/parishes`), usunięcie z `confirm`, paginacja/szukanie jeśli są;
- [ ] `budget` i `budget-dok`: dodanie operacji (`POST /api/budget` z `fund` SKSP/DOK, datą, opisem, kategorią, typem, kwotą), usunięcie z `confirm`, sumy przychodów/wydatków/saldo i struktura wydatków z listy (`totalIncome`, `totalExpense`, `balance`, `expenseByCategory`), błąd → toast;
- [ ] `mailing`: utworzenie kampanii (`POST /api/mailing/campaigns` z `subject`, `body`, `group`), wysyłka szkicu (`POST .../{id}/send`) ze zmianą statusu i toastem, błąd wysyłki pokazuje `err.error.title`, `groupLabels`;
- [ ] `documents`: generowanie (`POST /api/documents/generate` z `template`, `personId`, `additionalNotes`), przycisk „Generuj PDF” zablokowany bez osoby, historia odświeżona po generowaniu, błąd → toast;
- [ ] `idle-timeout.service`: po upływie 20 minut (`vi.useFakeTimers`) wywołuje `auth.logout('idle')`; aktywność (`mousedown`) resetuje licznik; `stop()` zdejmuje nasłuch i licznik.

- [ ] **Step 1:** Przeczytaj komponenty i istniejące spec-i; **Step 2:** napisz testy; **Step 3:** `npx ng test --watch=false --coverage`, sprawdź ≥ 80% linii dla tych plików; **Step 4:** commit `Testy frontendu: giełda, budżety, mailing, dokumenty i idle timeout`.

---

### Task 6: Luki backendu

**Files:**
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/{MissionService,SupervisionService,MeetingService,FormatorService,CaseDocumentService,UserService,PersonService,CandidateService}Tests.cs` (rozszerzyć istniejące lub utworzyć), `backend/tests/DokPortal.Api.IntegrationTests/{Formators,Meetings,CaseDocuments,Missions,Supervisions,Candidates,Users}ControllerTests.cs` (rozszerzyć)

**Interfaces:** Consumes: istniejące wzorce (`CreateContext`, `IntegrationTestBase.CreateAuthenticatedClientAsync`).

Zachowania do pokrycia (po przeczytaniu serwisu i istniejących testów danego modułu; raport: `dotnet test --collect:"XPlat Code Coverage" --results-directory <scratchpad>` + skrypt `cov.js` z scratchpada):
- [ ] serwisy `Update*Async`: istniejący rekord → zmienione pola widoczne w kolejnym odczycie, `UpdatedAtUtc` ustawione (jeśli encja je ma); nieistniejący id → `null`/`false` zgodnie z sygnaturą;
- [ ] serwisy `Delete*Async`: soft-delete (rekord znika z listy, `DeletedAtUtc` i `DeletedBy` ustawione), nieistniejący id → `false`; drugi raz usunięty rekord → `false`;
- [ ] `CaseDocumentService`: tworzenie pozycji, przełączanie `IsProvided`, wgranie pliku (zapis do `IFileStorageService` zastąpionego atrapą rejestrującą), pobranie, brak pliku → `null`;
- [ ] `UserService`: `AssignRolesAsync` zastępuje role, nieistniejący użytkownik → `null`; `ResetPasswordAsync` zmienia hasło (logowanie nowym hasłem), nieistniejący → `false`; `CreateAsync` z nieistniejącą osobą → `InvalidOperationException`;
- [ ] kontrolery: `PUT` i `DELETE` happy path (200/204), nieznany id → 404, rola bez uprawnienia → 403 (tam, gdzie nie ma jeszcze testu), niepoprawne ciało → 400.

- [ ] **Step 1:** Zmierz pokrycie i wskaż pliki < 80% linii; **Step 2:** napisz testy (jeden commit na grupę: serwisy, kontrolery); **Step 3:** `dotnet test` zielone, ponowny pomiar: backend ≥ 88% linii (bez migracji); **Step 4:** commity `Testy backendu: edycja i usuwanie w serwisach` i `Testy backendu: endpointy PUT/DELETE kontrolerów`.

---

### Task 7: Progi pokrycia i CI

**Files:**
- Modify: `frontend/angular.json` (`coverageThresholds`), `.github/workflows/frontend-ci.yml`, `.github/workflows/backend-ci.yml`

**Interfaces:** Consumes: wyniki pomiaru z Tasks 2–6.

- [ ] **Step 1: Pomiar końcowy**

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false --coverage 2>&1 | grep -E "Statements|Branches|Functions|Lines"
```

- [ ] **Step 2: Progi** — do opcji `test` w `angular.json` dodaj (wartości = osiągnięty poziom zaokrąglony w dół i pomniejszony o 1–2 p.p.; przykład dla poziomu 82/74/80/83):

```json
            "coverageThresholds": {
              "statements": 80,
              "branches": 72,
              "functions": 78,
              "lines": 81
            }
```
Zweryfikuj, że `npx ng test --watch=false --coverage` kończy się kodem 0, a po tymczasowym podniesieniu progu `lines` do 99 kończy się błędem (następnie przywróć właściwą wartość).

- [ ] **Step 3: CI** — w `frontend-ci.yml` zamień krok `- run: npx ng test` na `- run: npx ng test --coverage`; w `backend-ci.yml` zamień `- run: dotnet test --no-build --configuration Release` na:

```yaml
      - run: dotnet test --no-build --configuration Release --collect:"XPlat Code Coverage"
```

- [ ] **Step 4: Pełna weryfikacja**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test 2>&1 | grep -E "error|Powodzenie|niepowodzenie"
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false --coverage 2>&1 | grep -E "ERROR|FAIL|Test Files|Tests |Lines|Functions"; npx ng build 2>&1 | grep -iE "error|Application bundle"
```

- [ ] **Step 5: Commit (bez push)**

```bash
cd C:/eu02_install/DOKPortalLight && git add frontend/angular.json .github/workflows/frontend-ci.yml .github/workflows/backend-ci.yml docs/superpowers/specs/2026-10-03-test-quality-design.md docs/superpowers/plans/2026-10-03-test-quality.md
git commit -m "$(cat <<'EOF'
CI: pomiar pokrycia i progi dla frontendu

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

## Self-review

- **Pokrycie specyfikacji:** narzędzia i opcje buildera, wyłączenia, reportery, progi, CI frontend i backend — Tasks 1 i 7; pomoc testowa — Task 1; testy zachowania w kolejności ryzyka (użytkownicy/logowanie, sprawy DOK, Osoby, listy SKŚP/DOK, giełda/budżety/mailing/dokumenty, idle timeout) — Tasks 2–5; luki backendu (serwisy, kontrolery) — Task 6; kryteria liczbowe weryfikowane pomiarem w Tasks 2–7.
- **Świadome odstępstwo od „bez placeholderów”:** w Tasks 2–6 kod testów komponentów nie jest wypisany, bo zależy od źródeł, które czyta się przy każdym zadaniu; plan wymienia dokładnie, jakie zachowania (żądanie HTTP, ciało, efekt widoczny) mają być pokryte i jak to zweryfikować pomiarem pokrycia.
- **Spójność typów:** API pomocy testowej (`setup`, `api`, `paged`, `flushAll`, `click`, `clickByText`, `setInput`, `setSelect`, `textOf`, `provideFakeAuth`) zdefiniowane w Task 1 i używane w kolejnych bez zmian.
