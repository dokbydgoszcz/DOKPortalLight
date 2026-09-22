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
});
