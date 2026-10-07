import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Resource, ResourceFormValue } from './resource.model';

@Injectable({ providedIn: 'root' })
export class ResourcesService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/resources`;

  constructor(private readonly http: HttpClient) {}

  list(query?: string) {
    return this.http.get<Resource[]>(this.baseUrl, { params: query ? { query } : {} });
  }

  create(value: ResourceFormValue) {
    return this.http.post<Resource>(this.baseUrl, value);
  }

  update(id: string, value: ResourceFormValue) {
    return this.http.put<Resource>(`${this.baseUrl}/${id}`, value);
  }

  delete(id: string) {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Adres bazowy plików zasobu – dla komponentu załączników. */
  attachmentsUrl(id: string): string {
    return `${this.baseUrl}/${id}/attachments`;
  }
}
