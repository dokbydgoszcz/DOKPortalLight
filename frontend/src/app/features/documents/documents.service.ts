import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map } from 'rxjs';
import { readBlobErrors } from '../../shared/http-error';
import { environment } from '../../../environments/environment';
import {
  GeneratedDocument, GenerateDocumentValue, LetterField, LetterFormat, LetterTemplate, Letterhead, SaveLetterTemplate, downloadedFile
} from './generated-document.model';

@Injectable({ providedIn: 'root' })
export class DocumentsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/documents`;
  private readonly templatesUrl = `${environment.apiBaseUrl}/api/document-templates`;

  constructor(private readonly http: HttpClient) {}

  history() {
    return this.http.get<GeneratedDocument[]>(this.baseUrl);
  }

  download(id: string, format: LetterFormat = 'pdf') {
    return this.http.get(`${this.baseUrl}/${id}/download`, { params: { format }, responseType: 'blob', observe: 'response' })
      .pipe(readBlobErrors(), map(r => downloadedFile(r, `pismo.${format}`)));
  }

  delete(id: string) {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  generate(value: GenerateDocumentValue) {
    return this.http.post(`${this.baseUrl}/generate`, value, { responseType: 'blob', observe: 'response' })
      .pipe(readBlobErrors(), map(r => downloadedFile(r, `pismo.${value.format}`)));
  }

  templates(all = false) {
    return this.http.get<LetterTemplate[]>(this.templatesUrl, { params: all ? { all: 'true' } : {} });
  }

  fields() {
    return this.http.get<LetterField[]>(`${this.templatesUrl}/fields`);
  }

  createTemplate(value: SaveLetterTemplate) {
    return this.http.post<LetterTemplate>(this.templatesUrl, value);
  }

  updateTemplate(id: string, value: SaveLetterTemplate) {
    return this.http.put<LetterTemplate>(`${this.templatesUrl}/${id}`, value);
  }

  deleteTemplate(id: string) {
    return this.http.delete<void>(`${this.templatesUrl}/${id}`);
  }

  preview(body: string, useLetterhead: boolean, personId: string | null, format: LetterFormat = 'pdf', inputs?: Record<string, string>) {
    return this.http.post(`${this.templatesUrl}/preview`, { body, useLetterhead, personId, format, inputs },
      { responseType: 'blob', observe: 'response' }).pipe(readBlobErrors(), map(r => downloadedFile(r, `podglad.${format}`)));
  }

  letterhead() {
    return this.http.get<Letterhead>(`${this.templatesUrl}/letterhead`);
  }

  saveLetterhead(value: { headerText: string; footerText: string; place: string }) {
    return this.http.put<Letterhead>(`${this.templatesUrl}/letterhead`, value);
  }

  logo() {
    return this.http.get(`${this.templatesUrl}/letterhead/logo`, { responseType: 'blob' });
  }

  uploadLogo(file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.http.put<Letterhead>(`${this.templatesUrl}/letterhead/logo`, form);
  }

  deleteLogo() {
    return this.http.delete<Letterhead>(`${this.templatesUrl}/letterhead/logo`);
  }
}
