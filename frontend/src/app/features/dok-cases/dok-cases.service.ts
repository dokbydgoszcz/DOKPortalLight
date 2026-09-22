import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../people/person.model';
import { DokCase, DokCaseFormValue, DokPath } from './dok-case.model';

@Injectable({ providedIn: 'root' })
export class DokCasesService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/dok-cases`;

  constructor(private readonly http: HttpClient) {}

  search(path?: DokPath) {
    const params: Record<string, string> = { pageSize: '100' };
    if (path) {
      params['path'] = path;
    }
    return this.http.get<PagedResult<DokCase>>(this.baseUrl, { params });
  }

  create(value: DokCaseFormValue) {
    return this.http.post<DokCase>(this.baseUrl, value);
  }
}
