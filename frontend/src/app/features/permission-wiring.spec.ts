import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect } from 'vitest';
import { AuthService } from '../core/auth/auth.service';
import { environment } from '../../environments/environment';
import { PeopleListComponent } from './people/people-list.component';
import { CandidatesListComponent } from './candidates/candidates-list.component';
import { MissionsListComponent } from './missions/missions-list.component';
import { FormatorsListComponent } from './formators/formators-list.component';
import { MeetingsListComponent } from './meetings/meetings-list.component';
import { SupervisionsListComponent } from './supervisions/supervisions-list.component';
import { ParishBoardComponent } from './parish-board/parish-board.component';
import { BudgetComponent } from './budget/budget.component';
import { BudgetDokComponent } from './budget-dok/budget-dok.component';
import { DokCasesListComponent } from './dok-cases/dok-cases-list.component';
import { MailingComponent } from './mailing/mailing.component';
import { DocumentsComponent } from './documents/documents.component';

interface HeaderCase {
  name: string;
  component: Type<unknown>;
  permission: string;
  label: string;
}

const HEADER_CASES: HeaderCase[] = [
  { name: 'people', component: PeopleListComponent, permission: 'People.Manage', label: '＋ Dodaj osobę' },
  { name: 'candidates', component: CandidatesListComponent, permission: 'Candidates.Manage', label: '＋ Nowy kandydat' },
  { name: 'missions', component: MissionsListComponent, permission: 'Missions.Manage', label: '＋ Dodaj misję' },
  { name: 'formators', component: FormatorsListComponent, permission: 'Formators.Manage', label: '＋ Dodaj formatora' },
  { name: 'meetings', component: MeetingsListComponent, permission: 'Meetings.Manage', label: '＋ Dodaj spotkanie' },
  { name: 'supervisions', component: SupervisionsListComponent, permission: 'Supervisions.Manage', label: '＋ Nowa superwizja' },
  { name: 'parish-board', component: ParishBoardComponent, permission: 'ParishNeeds.Manage', label: '＋ Nowe zapotrzebowanie' },
  { name: 'budget-sksp', component: BudgetComponent, permission: 'BudgetSksp.Manage', label: '＋ Dodaj operację' },
  { name: 'budget-dok', component: BudgetDokComponent, permission: 'BudgetDok.Manage', label: '＋ Dodaj operację' },
  { name: 'dok-cases', component: DokCasesListComponent, permission: 'DokCases.Manage', label: '＋ Nowy podopieczny' },
  { name: 'mailing', component: MailingComponent, permission: 'Mailing.Manage', label: '＋ Nowa kampania' },
  { name: 'documents', component: DocumentsComponent, permission: 'Documents.Generate', label: 'Generuj PDF' }
];

function render(component: Type<unknown>, granted: string[]) {
  TestBed.configureTestingModule({
    imports: [component],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: AuthService, useValue: { hasPermission: (p: string) => granted.includes(p), roles: () => [] } }
    ]
  });
  const fixture = TestBed.createComponent(component);
  fixture.detectChanges();
  return fixture;
}

describe('action buttons are gated by permissions', () => {
  for (const c of HEADER_CASES) {
    it(`${c.name}: header button is hidden without ${c.permission}`, () => {
      const fixture = render(c.component, []);

      expect(fixture.nativeElement.textContent as string).not.toContain(c.label);
    });

    it(`${c.name}: header button is visible with ${c.permission}`, () => {
      const fixture = render(c.component, [c.permission]);

      expect(fixture.nativeElement.textContent as string).toContain(c.label);
    });
  }

  it('people: row actions Edytuj/Usuń follow People.Manage', () => {
    const person = { id: '1', firstName: 'Anna', lastName: 'Maj', fullName: 'Anna Maj', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null };
    for (const [granted, expected] of [[[], false], [['People.Manage'], true]] as const) {
      TestBed.resetTestingModule();
      const fixture = render(PeopleListComponent, [...granted]);
      TestBed.inject(HttpTestingController)
        .expectOne(r => r.url === `${environment.apiBaseUrl}/api/people`)
        .flush({ items: [person], totalCount: 1, page: 1, pageSize: 20 });
      fixture.detectChanges();

      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Anna Maj');
      expect(text.includes('Edytuj')).toBe(expected);
      expect(text.includes('Usuń')).toBe(expected);
    }
  });

  it('dok-cases: row actions are gated independently', () => {
    const dokCase = { id: '1', personId: 'p1', personFullName: 'Jan Kowalski', parishName: null, path: 'Confirmation', stage: 'Formation', catechistPersonId: 'c1', catechistFullName: 'Anna Maj', mentorPersonId: null, mentorFullName: null, lastMeetingDate: null, completedAtUtc: null };
    const scenarios: Array<[string[], boolean, boolean, boolean]> = [
      [[], false, false, false],
      [['PastoralNotes.View'], true, false, false],
      [['CaseDocuments.View'], false, true, false],
      [['DokCases.Manage'], false, false, true]
    ];
    for (const [granted, notes, documents, remove] of scenarios) {
      TestBed.resetTestingModule();
      const fixture = render(DokCasesListComponent, granted);
      const http = TestBed.inject(HttpTestingController);
      for (const req of http.match(r => r.url === `${environment.apiBaseUrl}/api/dok-cases`)) {
        req.flush({ items: [dokCase], totalCount: 1, page: 1, pageSize: 20 });
      }
      http.match(r => r.url === `${environment.apiBaseUrl}/api/people`)
        .forEach(req => req.flush({ items: [], totalCount: 0, page: 1, pageSize: 200 }));
      fixture.detectChanges();

      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Jan Kowalski');
      expect(text.includes('Notatki')).toBe(notes);
      expect(text.includes('Dokumenty')).toBe(documents);
      expect(text.includes('Usuń')).toBe(remove);
    }
  });
});
