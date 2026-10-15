import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Parish, ParishImportResult } from './parish-need.model';

@Injectable({ providedIn: 'root' })
export class ParishesService {
  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<Parish[]>(`${environment.apiBaseUrl}/api/parishes`);
  }

  create(value: ParishFormValue) {
    return this.http.post<Parish>(`${environment.apiBaseUrl}/api/parishes`, value);
  }

  update(id: string, value: ParishFormValue) {
    return this.http.put<Parish>(`${environment.apiBaseUrl}/api/parishes/${id}`, value);
  }

  /** Import spisu parafii z proboszczami z arkusza Excel; apply = false tylko pokazuje, co się stanie. */
  importPastors(file: File, apply: boolean) {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ParishImportResult>(`${environment.apiBaseUrl}/api/parishes/import${apply ? '' : '/preview'}`, form);
  }

  delete(id: string) {
    return this.http.delete<void>(`${environment.apiBaseUrl}/api/parishes/${id}`);
  }
}

/** Dane formularza parafii; proboszcz zawsze wysyłany (null = parafia bez proboszcza). */
export interface ParishFormValue {
  name: string;
  city?: string;
  pastorPersonId: string | null;
  deanery?: string;
  address?: string;
  phone?: string;
  email?: string;
  website?: string;
}
