import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { BudgetComponent } from './budget.component';
import { environment } from '../../../environments/environment';

describe('BudgetComponent', () => {
  let fixture: ComponentFixture<BudgetComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [BudgetComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(BudgetComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('computes income, expense, and balance totals from the fetched entries', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/budget`);
    req.flush([
      { id: '1', fund: 'SKSP', entryDate: '2026-09-12', description: 'Dotacja', category: 'Dotacja', type: 'Income', amount: 5000 },
      { id: '2', fund: 'SKSP', entryDate: '2026-09-18', description: 'Materiały', category: 'Materiały', type: 'Expense', amount: 780 }
    ]);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('5000');
    expect(text).toContain('780');
    expect(text).toContain('4220');
  });
});
