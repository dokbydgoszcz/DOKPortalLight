import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { ExportButtonComponent } from '../../shared/export/export-button.component';
import { Component, OnInit, computed, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MissionsService } from './missions.service';
import { Mission, MissionFormValue, PendingCatechist } from './mission.model';
import { MissionFormComponent } from './mission-form.component';
import { ToastService } from '../../core/notifications/toast.service';
import { PaginationComponent } from '../../shared/pagination.component';
import { AttachmentsComponent } from '../../shared/attachments/attachments.component';
import { serverMessage } from '../../shared/http-error';
import { environment } from '../../../environments/environment';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-missions-list',
  standalone: true,
  imports: [HasPermissionDirective, ExportButtonComponent, MissionFormComponent, PaginationComponent, AttachmentsComponent, DatePipe],
  templateUrl: './missions-list.component.html',
  styleUrl: './missions-list.component.scss'
})
export class MissionsListComponent implements OnInit {
  readonly missions = signal<Mission[]>([]);
  /** Osoby, które ukończyły formację i czekają na udzielenie posługi. */
  readonly pending = signal<PendingCatechist[]>([]);
  /** Misja, której okno załączników jest otwarte; dane bierze z listy, więc odświeżają się po każdym przeładowaniu. */
  readonly attachmentsMissionId = signal<string | null>(null);
  readonly attachmentsMission = computed(() => this.missions().find(m => m.id === this.attachmentsMissionId()) ?? null);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = PAGE_SIZE;
  readonly isFormOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  formValue: MissionFormValue = { personId: '', servicePlace: '', missionStartDate: '', missionEndDate: '', sentToDok: false };

  constructor(
    private readonly missionsService: MissionsService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
    this.loadPending();
  }

  load(): void {
    this.missionsService.search('', this.page(), this.pageSize).subscribe({
      next: result => {
        this.missions.set(result.items);
        this.totalCount.set(result.totalCount);
      },
      error: () => this.toast.error('Nie udało się wczytać listy misji.')
    });
  }

  loadPending(): void {
    this.missionsService.pending().subscribe({
      next: pending => this.pending.set(pending),
      error: () => this.toast.error('Nie udało się wczytać listy oczekujących na posłanie.')
    });
  }

  grant(person: PendingCatechist): void {
    this.missionsService.grant(person.personId).subscribe({
      next: () => {
        this.toast.success('Udzielono posłania. Uzupełnij miejsce posługi i daty w „Edytuj”, a dokument posłania dołącz w „Załączniki”.');
        this.load();
        this.loadPending();
      },
      error: err => this.toast.error(serverMessage(err, 'Nie udało się udzielić posłania.'))
    });
  }

  attachmentsUrl(mission: Mission): string {
    return `${environment.apiBaseUrl}/api/missions/${mission.id}/attachments`;
  }

  openAttachments(mission: Mission): void {
    this.attachmentsMissionId.set(mission.id);
  }

  closeAttachments(): void {
    this.attachmentsMissionId.set(null);
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }

  openAddForm(): void {
    this.editingId.set(null);
    this.formValue = { personId: '', servicePlace: '', missionStartDate: '', missionEndDate: '', sentToDok: false };
    this.isFormOpen.set(true);
  }

  openEditForm(mission: Mission): void {
    this.editingId.set(mission.id);
    this.formValue = {
      personId: mission.personId,
      servicePlace: mission.servicePlace,
      missionStartDate: mission.missionStartDate,
      missionEndDate: mission.missionEndDate,
      grantedDate: mission.grantedDate ?? undefined,
      supervisionGroup: mission.supervisionGroup ?? undefined,
      sentToDok: mission.sentToDok
    };
    this.isFormOpen.set(true);
  }

  onSave(value: MissionFormValue): void {
    const id = this.editingId();
    const request$ = id ? this.missionsService.update(id, value) : this.missionsService.create(value);
    request$.subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success(id ? 'Zapisano zmiany.' : 'Dodano misję.');
        this.load();
      },
      error: () => this.toast.error(id ? 'Nie udało się zapisać zmian.' : 'Nie udało się dodać misji.')
    });
  }

  onCancel(): void {
    this.isFormOpen.set(false);
  }

  deleteMission(mission: Mission): void {
    if (!confirm(`Usunąć misję „${mission.personFullName}”?`)) return;
    this.missionsService.delete(mission.id).subscribe({
      next: () => {
        this.toast.success('Misja usunięta.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się usunąć misji.')
    });
  }

  statusPillClass(status: string): string {
    if (status === 'wygasła') return 'pill red';
    if (status === 'wygasa') return 'pill red';
    return 'pill green';
  }
}
