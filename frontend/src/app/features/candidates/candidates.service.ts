import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../people/person.model';
import { AdvanceResult, Candidate, CandidateFormValue } from './candidate.model';

@Injectable({ providedIn: 'root' })
export class CandidatesService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/candidates`;

  constructor(private readonly http: HttpClient) {}

  search(year?: number, page = 1, pageSize = 20) {
    const params: Record<string, string> = { page: String(page), pageSize: String(pageSize) };
    if (year) {
      params['year'] = String(year);
    }
    return this.http.get<PagedResult<Candidate>>(this.baseUrl, { params });
  }

  create(value: CandidateFormValue) {
    return this.http.post<Candidate>(this.baseUrl, value);
  }

  update(id: string, value: CandidateFormValue) {
    return this.http.put<Candidate>(`${this.baseUrl}/${id}`, value);
  }

  /** Przenosi kandydatów o rok dalej (z III roku: kończy formację). */
  advance(candidateIds: string[]) {
    return this.http.post<AdvanceResult>(`${this.baseUrl}/advance`, { candidateIds });
  }

  delete(id: string) {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
