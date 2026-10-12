import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../people/person.model';
import { DokCase, DokCaseEvent, DokCaseFormValue, DokPath, DokStage } from './dok-case.model';

@Injectable({ providedIn: 'root' })
export class DokCasesService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/dok-cases`;

  constructor(private readonly http: HttpClient) {}

  search(path?: DokPath, page = 1, pageSize = 20, stage?: DokStage) {
    const params: Record<string, string> = { page: String(page), pageSize: String(pageSize) };
    if (path) {
      params['path'] = path;
    }
    if (stage) {
      params['stage'] = stage;
    }
    return this.http.get<PagedResult<DokCase>>(this.baseUrl, { params });
  }

  create(value: DokCaseFormValue) {
    return this.http.post<DokCase>(this.baseUrl, value);
  }

  update(id: string, value: DokCaseFormValue) {
    return this.http.put<DokCase>(`${this.baseUrl}/${id}`, value);
  }

  history(id: string) {
    return this.http.get<DokCaseEvent[]>(`${this.baseUrl}/${id}/history`);
  }

  delete(id: string) {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
