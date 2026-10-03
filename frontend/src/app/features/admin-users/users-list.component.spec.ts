import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { UsersListComponent } from './users-list.component';
import { environment } from '../../../environments/environment';

describe('UsersListComponent', () => {
  let fixture: ComponentFixture<UsersListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [UsersListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(UsersListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders emails returned from the API', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/users`);
    req.flush([{ id: '1', email: 'admin@dokportal.local', personId: null, roles: ['Administrator'] }]);
    fixture.detectChanges();

    expect((fixture.nativeElement.textContent as string)).toContain('admin@dokportal.local');
  });

  it('renders one role column per role returned by the API', () => {
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiBaseUrl}/api/users`)
      .flush([{ id: '1', email: 'admin@dokportal.local', personId: null, roles: ['Administrator'] }]);
    httpMock.expectOne(`${environment.apiBaseUrl}/api/users/roles`).flush(['Administrator', 'Sekretariat']);
    fixture.detectChanges();

    const headers = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('thead th')).map(th => th.textContent!.trim());
    expect(headers).toContain('Sekretariat');
    expect(fixture.nativeElement.querySelectorAll('tbody input[type="checkbox"]').length).toBe(2);
  });
});
