import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { UsersService } from './users.service';
import { environment } from '../../../environments/environment';

describe('UsersService', () => {
  it('requests the user list from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(UsersService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.list().subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/users`);
    req.flush([]);
    httpMock.verify();
  });
});
