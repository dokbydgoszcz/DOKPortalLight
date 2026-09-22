import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { BudgetDokComponent } from './budget-dok.component';
import { environment } from '../../../environments/environment';

describe('BudgetDokComponent', () => {
  let fixture: ComponentFixture<BudgetDokComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [BudgetDokComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(BudgetDokComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('requests DOK-fund entries and computes totals', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(
      r => r.url === `${environment.apiBaseUrl}/api/budget` && r.params.get('fund') === 'DOK'
    );
    req.flush([
      { id: '1', fund: 'DOK', entryDate: '2026-09-12', description: 'Dotacja diecezjalna', category: 'Dotacja', type: 'Income', amount: 5000 },
      { id: '2', fund: 'DOK', entryDate: '2026-09-18', description: 'Materiały formacyjne', category: 'Materiały', type: 'Expense', amount: 780 }
    ]);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Dotacja diecezjalna');
  });
});
