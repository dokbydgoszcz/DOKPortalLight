import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../people/person.model';
import { Candidate, CandidateFormValue } from './candidate.model';

@Injectable({ providedIn: 'root' })
export class CandidatesService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/candidates`;

  constructor(private readonly http: HttpClient) {}

  search(year?: number) {
    const params: Record<string, string> = { pageSize: '100' };
    if (year) {
      params['year'] = String(year);
    }
    return this.http.get<PagedResult<Candidate>>(this.baseUrl, { params });
  }

  create(value: CandidateFormValue) {
    return this.http.post<Candidate>(this.baseUrl, value);
  }
}
