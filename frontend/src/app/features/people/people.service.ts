import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { FunctionType, PagedResult, Person, PersonFormValue } from './person.model';

@Injectable({ providedIn: 'root' })
export class PeopleService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/people`;

  constructor(private readonly http: HttpClient) {}

  search(query: string, page = 1, pageSize = 20, fn?: FunctionType) {
    const params: Record<string, string | number> = { query, page, pageSize };
    if (fn) params['function'] = fn;
    return this.http.get<PagedResult<Person>>(this.baseUrl, { params });
  }

  getById(id: string) {
    return this.http.get<Person>(`${this.baseUrl}/${id}`);
  }

  create(value: PersonFormValue) {
    return this.http.post<Person>(this.baseUrl, value);
  }

  update(id: string, value: PersonFormValue) {
    return this.http.put<Person>(`${this.baseUrl}/${id}`, value);
  }

  delete(id: string) {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
