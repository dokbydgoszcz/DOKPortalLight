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

  send(id: string) {
    return this.http.post<MailingCampaign>(`${this.baseUrl}/${id}/send`, {});
  }
}
