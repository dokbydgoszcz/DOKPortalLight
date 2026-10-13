import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { CreateMailingCampaignValue, MailingCampaign, MailingGroup, MailingRecipient } from './mailing-campaign.model';

@Injectable({ providedIn: 'root' })
export class MailingService {
  readonly baseUrl = `${environment.apiBaseUrl}/api/mailing/campaigns`;

  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<MailingCampaign[]>(this.baseUrl);
  }

  create(value: CreateMailingCampaignValue) {
    return this.http.post<MailingCampaign>(this.baseUrl, value);
  }

  update(id: string, value: CreateMailingCampaignValue) {
    return this.http.put<MailingCampaign>(`${this.baseUrl}/${id}`, value);
  }

  deleteDraft(id: string) {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Adresaci grupy przed zapisaniem kampanii. */
  groupRecipients(group: MailingGroup) {
    return this.http.get<MailingRecipient[]>(`${environment.apiBaseUrl}/api/mailing/recipients`, { params: { group } });
  }

  campaignRecipients(id: string) {
    return this.http.get<MailingRecipient[]>(`${this.baseUrl}/${id}/recipients`);
  }

  /** Wysyła wiadomość testową na adres zalogowanego użytkownika (sprawdzenie ustawień SMTP). */
  sendTest() {
    return this.http.post<{ sentTo: string }>(`${environment.apiBaseUrl}/api/mailing/test-email`, {});
  }

  send(id: string) {
    return this.http.post<MailingCampaign>(`${this.baseUrl}/${id}/send`, {});
  }
}
