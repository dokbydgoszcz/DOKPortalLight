import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { GeneratedDocument, GenerateDocumentValue } from './generated-document.model';

@Injectable({ providedIn: 'root' })
export class DocumentsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/documents`;

  constructor(private readonly http: HttpClient) {}

  history() {
    return this.http.get<GeneratedDocument[]>(this.baseUrl);
  }

  download(id: string) {
    return this.http.get(`${this.baseUrl}/${id}/download`, { responseType: 'blob' });
  }

  delete(id: string) {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  generate(value: GenerateDocumentValue) {
    return this.http.post(`${this.baseUrl}/generate`, value, { responseType: 'blob' });
  }
}
