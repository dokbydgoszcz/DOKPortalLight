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

  generate(value: GenerateDocumentValue) {
    return this.http.post(`${this.baseUrl}/generate`, value, { responseType: 'blob' });
  }
}
