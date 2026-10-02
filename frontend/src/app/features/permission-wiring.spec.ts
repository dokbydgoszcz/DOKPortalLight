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
  { name: 'supervisions', component: SupervisionsListComponent, permission: 'Supervisions.Manage', label: '＋ Nowa superwizja' }
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
});
