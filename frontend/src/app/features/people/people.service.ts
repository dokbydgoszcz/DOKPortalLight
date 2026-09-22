import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { PagedResult, Person, PersonFormValue } from './person.model';

@Injectable({ providedIn: 'root' })
export class PeopleService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/people`;

  constructor(private readonly http: HttpClient) {}

  search(query: string, page = 1, pageSize = 20) {
    return this.http.get<PagedResult<Person>>(this.baseUrl, { params: { query, page, pageSize } });
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
}
