import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { CreateMailingCampaignValue, MailingCampaign } from './mailing-campaign.model';

@Injectable({ providedIn: 'root' })
export class MailingService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/mailing/campaigns`;

  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<MailingCampaign[]>(this.baseUrl);
  }

  create(value: CreateMailingCampaignValue) {
    return this.http.post<MailingCampaign>(this.baseUrl, value);
  }

  deleteDraft(id: string) {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Wysyła wiadomość testową na adres zalogowanego użytkownika (sprawdzenie ustawień SMTP). */
  sendTest() {
    return this.http.post<{ sentTo: string }>(`${environment.apiBaseUrl}/api/mailing/test-email`, {});
  }

  send(id: string) {
    return this.http.post<MailingCampaign>(`${this.baseUrl}/${id}/send`, {});
  }
}
