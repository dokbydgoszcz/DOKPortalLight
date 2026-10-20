import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { readBlobErrors } from '../../shared/http-error';
import { ApplyFormSubmission, FormSubmission, FormSubmissionStatus } from './form-submission.model';

@Injectable({ providedIn: 'root' })
export class FormSubmissionsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/form-submissions`;

  list(status: FormSubmissionStatus) {
    return this.http.get<FormSubmission[]>(this.baseUrl, { params: { status } });
  }

  pendingCount() {
    return this.http.get<number>(`${this.baseUrl}/pending-count`);
  }

  apply(id: string, body: ApplyFormSubmission) {
    return this.http.post<FormSubmission>(`${this.baseUrl}/${id}/apply`, body);
  }

  reject(id: string) {
    return this.http.post<FormSubmission>(`${this.baseUrl}/${id}/reject`, {});
  }

  reopen(id: string) {
    return this.http.post<FormSubmission>(`${this.baseUrl}/${id}/reopen`, {});
  }

  /** Plik ze zgłoszenia (np. zdjęcie) – serwer pobiera go z Tally, bo linki Tally są prywatne. */
  file(id: string, index: number) {
    return this.http.get(`${this.baseUrl}/${id}/files/${index}`, { responseType: 'blob' }).pipe(readBlobErrors());
  }
}
