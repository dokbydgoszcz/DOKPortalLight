import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { CreateSupervisionValue, Institution, Supervision } from './supervision.model';

@Injectable({ providedIn: 'root' })
export class SupervisionsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/supervisions`;

  constructor(private readonly http: HttpClient) {}

  list(institution?: Institution) {
    const params: Record<string, string> = {};
    if (institution) {
      params['institution'] = institution;
    }
    return this.http.get<Supervision[]>(this.baseUrl, { params });
  }

  create(value: CreateSupervisionValue) {
    return this.http.post<Supervision>(this.baseUrl, value);
  }
}
