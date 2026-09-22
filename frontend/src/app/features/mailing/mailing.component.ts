import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MailingService } from './mailing.service';
import { CreateMailingCampaignValue, MAILING_GROUP_LABELS, MailingCampaign, MailingGroup } from './mailing-campaign.model';

@Component({
  selector: 'app-mailing',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './mailing.component.html',
  styleUrl: './mailing.component.scss'
})
export class MailingComponent implements OnInit {
  readonly campaigns = signal<MailingCampaign[]>([]);
  readonly isFormOpen = signal(false);
  readonly groupLabels = MAILING_GROUP_LABELS;
  readonly groups = Object.keys(MAILING_GROUP_LABELS) as MailingGroup[];
  newCampaign: CreateMailingCampaignValue = { subject: '', body: '', group: 'CandidatesSksp' };

  constructor(private readonly mailingService: MailingService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.mailingService.list().subscribe(campaigns => this.campaigns.set(campaigns));
  }

  openAddForm(): void {
    this.newCampaign = { subject: '', body: '', group: 'CandidatesSksp' };
    this.isFormOpen.set(true);
  }

  createCampaign(): void {
    this.mailingService.create(this.newCampaign).subscribe(() => {
      this.isFormOpen.set(false);
      this.load();
    });
  }

  sendCampaign(campaign: MailingCampaign): void {
    this.mailingService.send(campaign.id).subscribe(() => this.load());
  }

  cancel(): void {
    this.isFormOpen.set(false);
  }
}
