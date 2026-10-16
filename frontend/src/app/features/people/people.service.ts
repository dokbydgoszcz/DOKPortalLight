import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { FunctionType, PagedResult, Person, PersonFormValue } from './person.model';
import { PersonOverview } from './person-overview.model';

@Injectable({ providedIn: 'root' })
export class PeopleService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/people`;

  constructor(private readonly http: HttpClient) {}

  /** fn = 'none' – osoby bez żadnej funkcji. */
  search(query: string, page = 1, pageSize = 20, fn?: FunctionType | 'none') {
    const params: Record<string, string | number> = { query, page, pageSize };
    if (fn === 'none') params['withoutFunction'] = 'true';
    else if (fn) params['function'] = fn;
    return this.http.get<PagedResult<Person>>(this.baseUrl, { params });
  }

  getById(id: string) {
    return this.http.get<Person>(`${this.baseUrl}/${id}`);
  }

  /** Karta osoby: wszystkie powiązania w jednym miejscu. */
  overview(id: string) {
    return this.http.get<PersonOverview>(`${this.baseUrl}/${id}/overview`);
  }

  /** Zdjęcie profilowe (z nagłówkiem autoryzacji, dlatego jako plik, nie adres obrazka). */
  /** RODO: kopia wszystkich danych osoby (plik JSON). */
  gdprExport(id: string) {
    return this.http.get(`${this.baseUrl}/${id}/rodo/export`, { responseType: 'blob' });
  }

  /** RODO: nieodwracalne usunięcie danych osoby (anonimizacja). */
  anonymize(id: string) {
    return this.http.post<void>(`${this.baseUrl}/${id}/rodo/anonymize`, {});
  }

  photo(id: string) {
    return this.http.get(`${this.baseUrl}/${id}/photo`, { responseType: 'blob' });
  }

  uploadPhoto(id: string, file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.http.put<Person>(`${this.baseUrl}/${id}/photo`, form);
  }

  deletePhoto(id: string) {
    return this.http.delete<void>(`${this.baseUrl}/${id}/photo`);
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
