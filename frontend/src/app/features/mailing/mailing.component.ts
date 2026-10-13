import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MailingService } from './mailing.service';
import { CreateMailingCampaignValue, MAILING_GROUP_LABELS, MailingCampaign, MailingGroup, MailingRecipient } from './mailing-campaign.model';
import { ToastService } from '../../core/notifications/toast.service';
import { serverMessage } from '../../shared/http-error';
import { PeopleService } from '../people/people.service';
import { AttachmentsComponent } from '../../shared/attachments/attachments.component';
import { Permissions } from '../../core/auth/permissions';

@Component({
  selector: 'app-mailing',
  standalone: true,
  imports: [HasPermissionDirective, FormsModule, AttachmentsComponent],
  templateUrl: './mailing.component.html',
  styleUrl: './mailing.component.scss'
})
export class MailingComponent implements OnInit {
  readonly campaigns = signal<MailingCampaign[]>([]);
  readonly isFormOpen = signal(false);
  readonly isSendingTest = signal(false);
  /** Kampania, która właśnie jest wysyłana (blokuje przycisk, żeby podwójne kliknięcie nie wysłało jej dwa razy). */
  readonly sendingId = signal<string | null>(null);
  /** Szkic otwarty do poprawienia; pusty = nowa kampania. */
  readonly editing = signal<MailingCampaign | null>(null);
  readonly groupLabels = MAILING_GROUP_LABELS;
  readonly groups = Object.keys(MAILING_GROUP_LABELS) as MailingGroup[];
  readonly manage = Permissions.MailingManage;
  newCampaign: CreateMailingCampaignValue = { subject: '', body: '', group: 'CandidatesSksp' };

  /** Własna lista: wybrane osoby i wyniki wyszukiwania. */
  readonly picked = signal<MailingRecipient[]>([]);
  readonly searchResults = signal<MailingRecipient[]>([]);
  personQuery = '';

  /** Podgląd adresatów (w formularzu albo dla zapisanej kampanii). */
  readonly preview = signal<{ title: string; recipients: MailingRecipient[] } | null>(null);
  readonly previewWithEmail = computed(() => this.preview()?.recipients.filter(r => r.email).length ?? 0);

  readonly attachmentsCampaign = signal<MailingCampaign | null>(null);

  constructor(
    private readonly mailingService: MailingService,
    private readonly peopleService: PeopleService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  /** Wysyła wiadomość testową na adres zalogowanego użytkownika, żeby sprawdzić ustawienia SMTP. */
  sendTest(): void {
    if (this.isSendingTest()) return;
    this.isSendingTest.set(true);
    this.mailingService.sendTest().subscribe({
      next: result => {
        this.isSendingTest.set(false);
        this.toast.success(`Wysłano wiadomość testową na adres ${result.sentTo}.`);
      },
      error: err => {
        this.isSendingTest.set(false);
        this.toast.error(serverMessage(err, 'Nie udało się wysłać wiadomości testowej.'));
      }
    });
  }

  load(): void {
    this.mailingService.list().subscribe({
      next: campaigns => {
        this.campaigns.set(campaigns);
        const open = this.attachmentsCampaign();
        if (open) this.attachmentsCampaign.set(campaigns.find(c => c.id === open.id) ?? null);
      },
      error: () => this.toast.error('Nie udało się wczytać listy kampanii.')
    });
  }

  openAddForm(): void {
    this.editing.set(null);
    this.newCampaign = { subject: '', body: '', group: 'CandidatesSksp' };
    this.picked.set([]);
    this.searchResults.set([]);
    this.personQuery = '';
    this.isFormOpen.set(true);
  }

  openEditForm(campaign: MailingCampaign): void {
    this.editing.set(campaign);
    this.newCampaign = { subject: campaign.subject, body: campaign.body, group: campaign.group };
    this.picked.set([]);
    this.searchResults.set([]);
    this.personQuery = '';
    this.isFormOpen.set(true);
    if (campaign.group === 'Custom') {
      this.mailingService.campaignRecipients(campaign.id).subscribe({
        next: recipients => this.picked.set(recipients),
        error: () => this.toast.error('Nie udało się wczytać listy odbiorców.')
      });
    }
  }

  /** Kampania bez tematu lub treści (albo własna lista bez osób) nie ma sensu, więc zapis czeka. */
  get canSave(): boolean {
    const hasText = !!this.newCampaign.subject.trim() && !!this.newCampaign.body.trim();
    return hasText && (this.newCampaign.group !== 'Custom' || this.picked().length > 0);
  }

  private formValue(): CreateMailingCampaignValue {
    const value: CreateMailingCampaignValue = { subject: this.newCampaign.subject, body: this.newCampaign.body, group: this.newCampaign.group };
    if (value.group === 'Custom') value.personIds = this.picked().map(p => p.personId);
    return value;
  }

  createCampaign(): void {
    if (!this.canSave) return;
    const editing = this.editing();
    const request$ = editing ? this.mailingService.update(editing.id, this.formValue()) : this.mailingService.create(this.formValue());
    request$.subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success(editing ? 'Zapisano zmiany w szkicu.' : 'Dodano kampanię.');
        this.load();
      },
      error: err => this.toast.error(editing ? serverMessage(err, 'Nie udało się zapisać szkicu.') : 'Nie udało się dodać kampanii.')
    });
  }

  searchPeople(): void {
    const query = this.personQuery.trim();
    if (query.length < 2) {
      this.searchResults.set([]);
      return;
    }
    this.peopleService.search(query, 1, 20).subscribe({
      next: result => this.searchResults.set(result.items.map(p => ({ personId: p.id, fullName: p.fullName, email: p.email }))),
      error: () => this.toast.error('Nie udało się wyszukać osób.')
    });
  }

  isPicked(personId: string): boolean {
    return this.picked().some(p => p.personId === personId);
  }

  pick(person: MailingRecipient): void {
    if (!this.isPicked(person.personId)) this.picked.update(list => [...list, person]);
  }

  unpick(personId: string): void {
    this.picked.update(list => list.filter(p => p.personId !== personId));
  }

  previewForm(): void {
    const group = this.newCampaign.group;
    if (group === 'Custom') {
      this.preview.set({ title: MAILING_GROUP_LABELS[group], recipients: this.picked() });
      return;
    }
    this.mailingService.groupRecipients(group).subscribe({
      next: recipients => this.preview.set({ title: MAILING_GROUP_LABELS[group], recipients }),
      error: err => this.toast.error(serverMessage(err, 'Nie udało się wczytać odbiorców.'))
    });
  }

  previewCampaign(campaign: MailingCampaign): void {
    this.mailingService.campaignRecipients(campaign.id).subscribe({
      next: recipients => this.preview.set({ title: `${campaign.subject} — ${MAILING_GROUP_LABELS[campaign.group]}`, recipients }),
      error: err => this.toast.error(serverMessage(err, 'Nie udało się wczytać odbiorców.'))
    });
  }

  closePreview(): void {
    this.preview.set(null);
  }

  openAttachments(campaign: MailingCampaign): void {
    this.attachmentsCampaign.set(campaign);
  }

  closeAttachments(): void {
    this.attachmentsCampaign.set(null);
  }

  attachmentsUrl(campaign: MailingCampaign): string {
    return `${this.mailingService.baseUrl}/${campaign.id}/attachments`;
  }

  /** Pliki wysłanej kampanii są już tylko do pobrania. */
  attachmentsPermission(campaign: MailingCampaign): string {
    return campaign.status === 'Draft' ? this.manage : 'Mailing.SentIsReadOnly';
  }

  sendCampaign(campaign: MailingCampaign): void {
    if (this.sendingId()) return;
    this.sendingId.set(campaign.id);
    this.mailingService.send(campaign.id).subscribe({
      next: result => {
        this.sendingId.set(null);
        if (result.failedCount) {
          this.toast.error(`Wysłano do ${result.recipientCount} odbiorców. Nie udało się wysłać do ${result.failedCount} adresów – sprawdź ich poprawność.`);
        } else {
          this.toast.success('Kampania wysłana.');
        }
        this.load();
      },
      error: err => {
        this.sendingId.set(null);
        this.toast.error(err?.error?.title ?? 'Nie udało się wysłać kampanii.');
      }
    });
  }

  deleteDraft(campaign: MailingCampaign): void {
    if (!confirm(`Usunąć szkic kampanii „${campaign.subject}”?`)) return;
    this.mailingService.deleteDraft(campaign.id).subscribe({
      next: () => {
        this.toast.success('Szkic usunięty.');
        this.load();
      },
      error: err => this.toast.error(err?.error?.title ?? 'Nie udało się usunąć szkicu.')
    });
  }

  cancel(): void {
    this.isFormOpen.set(false);
  }
}
