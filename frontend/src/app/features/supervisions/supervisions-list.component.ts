import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { ExportButtonComponent } from '../../shared/export/export-button.component';
import { Component, OnInit, computed, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SupervisionsService } from './supervisions.service';
import { CreateSupervisionValue, Institution, Supervision, SupervisionAttendee, SupervisionGroup } from './supervision.model';
import { ToastService } from '../../core/notifications/toast.service';
import { AttachmentsComponent } from '../../shared/attachments/attachments.component';
import { environment } from '../../../environments/environment';

export type SupervisionSort = 'dateDesc' | 'dateAsc' | 'institution';

@Component({
  selector: 'app-supervisions-list',
  standalone: true,
  imports: [HasPermissionDirective, ExportButtonComponent, AttachmentsComponent, FormsModule, DatePipe],
  templateUrl: './supervisions-list.component.html',
  styleUrl: './supervisions-list.component.scss'
})
export class SupervisionsListComponent implements OnInit {
  readonly supervisions = signal<Supervision[]>([]);
  readonly institutionFilter = signal<Institution | ''>('');
  readonly sortBy = signal<SupervisionSort>('dateDesc');
  /** Lista w wybranej kolejności: po dacie (od najnowszych lub najstarszych) albo po instytucji, a w niej po dacie malejąco. */
  readonly sortedSupervisions = computed(() => {
    const byDate = (a: Supervision, b: Supervision) => a.supervisionDate.localeCompare(b.supervisionDate);
    const items = [...this.supervisions()];
    switch (this.sortBy()) {
      case 'dateAsc':
        return items.sort(byDate);
      case 'institution':
        return items.sort((a, b) => a.institution.localeCompare(b.institution) || byDate(b, a));
      default:
        return items.sort((a, b) => byDate(b, a));
    }
  });
  /** Superwizja, której okno załączników jest otwarte; dane bierze z listy, więc odświeżają się po każdym przeładowaniu. */
  readonly attachmentsSupervisionId = signal<string | null>(null);
  readonly attachmentsSupervision = computed(() => this.supervisions().find(s => s.id === this.attachmentsSupervisionId()) ?? null);
  readonly isFormOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  newSupervision: CreateSupervisionValue = { institution: 'DOK', groupLabel: '', supervisionDate: '' };
  /** Grupy superwizyjne z misji (podpowiedzi pola „Grupa” i lista katechistów). */
  readonly groups = signal<SupervisionGroup[]>([]);
  /** Lista obecności w formularzu; pusta = liczby wpisywane ręcznie. */
  readonly attendance = signal<SupervisionAttendee[]>([]);
  readonly presentCount = computed(() => this.attendance().filter(a => a.isPresent).length);

  constructor(
    private readonly supervisionsService: SupervisionsService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  onInstitutionFilterChange(value: Institution | ''): void {
    this.institutionFilter.set(value);
    this.load();
  }

  onSortChange(value: SupervisionSort): void {
    this.sortBy.set(value);
  }

  load(): void {
    const institution = this.institutionFilter();
    this.supervisionsService.list(institution || undefined).subscribe({
      next: supervisions => this.supervisions.set(supervisions),
      error: () => this.toast.error('Nie udało się wczytać listy superwizji.')
    });
  }

  attachmentsUrl(supervision: Supervision): string {
    return `${environment.apiBaseUrl}/api/supervisions/${supervision.id}/attachments`;
  }

  openAttachments(supervision: Supervision): void {
    this.attachmentsSupervisionId.set(supervision.id);
  }

  closeAttachments(): void {
    this.attachmentsSupervisionId.set(null);
  }

  private loadGroups(): void {
    this.supervisionsService.groups().subscribe({
      next: groups => {
        this.groups.set(groups);
        if (this.attendance().length === 0) this.onGroupChange();
      },
      error: () => this.groups.set([])
    });
  }

  /** Po wybraniu grupy z misji lista obecności to jej katechiści (zaznaczenia zostają dla osób już na liście). */
  onGroupChange(): void {
    const label = this.newSupervision.groupLabel.trim().toLowerCase();
    const group = this.groups().find(g => g.name.toLowerCase() === label);
    if (!group) return;
    const current = new Map(this.attendance().map(a => [a.personId, a.isPresent]));
    this.attendance.set(group.members.map(m => ({ personId: m.personId, fullName: m.fullName, isPresent: current.get(m.personId) ?? false })));
  }

  togglePresent(personId: string, present: boolean): void {
    this.attendance.update(list => list.map(a => (a.personId === personId ? { ...a, isPresent: present } : a)));
  }

  setAllPresent(present: boolean): void {
    this.attendance.update(list => list.map(a => ({ ...a, isPresent: present })));
  }

  presentNames(supervision: Supervision): string {
    return (supervision.attendance ?? []).filter(a => a.isPresent).map(a => a.fullName).join(', ');
  }

  openAddForm(): void {
    this.editingId.set(null);
    this.newSupervision = { institution: 'DOK', groupLabel: '', supervisionDate: '' };
    this.attendance.set([]);
    this.isFormOpen.set(true);
    this.loadGroups();
  }

  openEditForm(supervision: Supervision): void {
    this.editingId.set(supervision.id);
    this.newSupervision = {
      institution: supervision.institution,
      groupLabel: supervision.groupLabel,
      supervisionDate: supervision.supervisionDate,
      attendeesCount: supervision.attendeesCount ?? undefined,
      expectedCount: supervision.expectedCount ?? undefined,
      topic: supervision.topic ?? undefined,
      conclusion: supervision.conclusion ?? undefined
    };
    this.attendance.set(supervision.attendance ?? []);
    this.isFormOpen.set(true);
    this.loadGroups();
  }

  get canSave(): boolean {
    return !!this.newSupervision.supervisionDate && !!this.newSupervision.groupLabel.trim();
  }

  private formValue(): CreateSupervisionValue {
    const list = this.attendance();
    if (list.length === 0) return this.newSupervision;
    const { attendeesCount: _a, expectedCount: _e, ...rest } = this.newSupervision;
    return { ...rest, attendance: list.map(a => ({ personId: a.personId, isPresent: a.isPresent })) };
  }

  createSupervision(): void {
    if (!this.canSave) return;
    const id = this.editingId();
    const value = this.formValue();
    const request$ = id
      ? this.supervisionsService.update(id, value)
      : this.supervisionsService.create(value);
    request$.subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success(id ? 'Zapisano zmiany.' : 'Dodano superwizję.');
        this.load();
      },
      error: () => this.toast.error(id ? 'Nie udało się zapisać zmian.' : 'Nie udało się dodać superwizji.')
    });
  }

  cancel(): void {
    this.isFormOpen.set(false);
  }

  deleteSupervision(supervision: Supervision): void {
    if (!confirm(`Usunąć superwizję „${supervision.groupLabel}”?`)) return;
    this.supervisionsService.delete(supervision.id).subscribe({
      next: () => {
        this.toast.success('Superwizja usunięta.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się usunąć superwizji.')
    });
  }
}
