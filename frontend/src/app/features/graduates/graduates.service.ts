import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../people/person.model';
import { DokCase } from '../dok-cases/dok-case.model';

@Injectable({ providedIn: 'root' })
export class GraduatesService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/graduates`;

  constructor(private readonly http: HttpClient) {}

  search(search: string, page = 1, pageSize = 20) {
    return this.http.get<PagedResult<DokCase>>(this.baseUrl, { params: { search, page, pageSize } });
  }
}
