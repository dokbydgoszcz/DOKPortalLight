import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { PeopleListComponent } from './people-list.component';
import { environment } from '../../../environments/environment';

describe('PeopleListComponent', () => {
  let fixture: ComponentFixture<PeopleListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [PeopleListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(PeopleListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders people returned from the search endpoint', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/people`);
    req.flush({
      items: [{ id: '1', firstName: 'Anna', lastName: 'Maj', fullName: 'Anna Maj', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null }],
      totalCount: 1,
      page: 1,
      pageSize: 20
    });
    fixture.detectChanges();

    expect((fixture.nativeElement.textContent as string)).toContain('Anna Maj');
  });
});
