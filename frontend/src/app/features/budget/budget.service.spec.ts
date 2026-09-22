import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { BudgetService } from './budget.service';
import { environment } from '../../../environments/environment';

describe('BudgetService', () => {
  it('sends the fund as a request parameter when listing entries', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(BudgetService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.listEntries('SKSP').subscribe();

    const req = httpMock.expectOne(
      r => r.url === `${environment.apiBaseUrl}/api/budget` && r.params.get('fund') === 'SKSP'
    );
    req.flush([]);
    httpMock.verify();
  });
});
