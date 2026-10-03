import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MailingService } from './mailing.service';
import { CreateMailingCampaignValue, MAILING_GROUP_LABELS, MailingCampaign, MailingGroup } from './mailing-campaign.model';
import { ToastService } from '../../core/notifications/toast.service';

@Component({
  selector: 'app-mailing',
  standalone: true,
  imports: [HasPermissionDirective, FormsModule],
  templateUrl: './mailing.component.html',
  styleUrl: './mailing.component.scss'
})
export class MailingComponent implements OnInit {
  readonly campaigns = signal<MailingCampaign[]>([]);
  readonly isFormOpen = signal(false);
  readonly groupLabels = MAILING_GROUP_LABELS;
  readonly groups = Object.keys(MAILING_GROUP_LABELS) as MailingGroup[];
  newCampaign: CreateMailingCampaignValue = { subject: '', body: '', group: 'CandidatesSksp' };

  constructor(
    private readonly mailingService: MailingService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.mailingService.list().subscribe({
      next: campaigns => this.campaigns.set(campaigns),
      error: () => this.toast.error('Nie udało się wczytać listy kampanii.')
    });
  }

  openAddForm(): void {
    this.newCampaign = { subject: '', body: '', group: 'CandidatesSksp' };
    this.isFormOpen.set(true);
  }

  createCampaign(): void {
    this.mailingService.create(this.newCampaign).subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success('Dodano kampanię.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się dodać kampanii.')
    });
  }

  sendCampaign(campaign: MailingCampaign): void {
    this.mailingService.send(campaign.id).subscribe({
      next: () => {
        this.toast.success('Kampania wysłana.');
        this.load();
      },
      error: err => this.toast.error(err?.error?.title ?? 'Nie udało się wysłać kampanii.')
    });
  }

  cancel(): void {
    this.isFormOpen.set(false);
  }
}
