import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { SkspClass, SkspClassFormValue } from './sksp-class.model';

@Injectable({ providedIn: 'root' })
export class SkspClassesService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/sksp-classes`;

  constructor(private readonly http: HttpClient) {}

  list(year?: number) {
    return this.http.get<SkspClass[]>(this.baseUrl, year ? { params: { year: String(year) } } : {});
  }

  create(value: SkspClassFormValue) {
    return this.http.post<SkspClass>(this.baseUrl, value);
  }

  update(id: string, value: SkspClassFormValue) {
    return this.http.put<SkspClass>(`${this.baseUrl}/${id}`, value);
  }

  setAttendance(id: string, items: { candidateId: string; isPresent: boolean | null }[]) {
    return this.http.put<SkspClass>(`${this.baseUrl}/${id}/attendance`, items);
  }

  refreshAttendees(id: string) {
    return this.http.post<SkspClass>(`${this.baseUrl}/${id}/refresh-attendees`, {});
  }

  delete(id: string) {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
