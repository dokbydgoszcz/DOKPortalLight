import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { DokCasesService } from './dok-cases.service';
import { environment } from '../../../environments/environment';

describe('DokCasesService', () => {
  it('sends the path as a request parameter when searching', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(DokCasesService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.search('Confirmation').subscribe();

    const req = httpMock.expectOne(
      r => r.url === `${environment.apiBaseUrl}/api/dok-cases` && r.params.get('path') === 'Confirmation'
    );
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 100 });
    httpMock.verify();
  });
});
