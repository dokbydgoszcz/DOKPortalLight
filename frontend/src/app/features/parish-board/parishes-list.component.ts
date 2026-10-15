import { ExportButtonComponent } from '../../shared/export/export-button.component';
import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ParishFormValue, ParishesService } from './parishes.service';
import { RouterLink } from '@angular/router';
import { PeopleService } from '../people/people.service';
import { Parish, ParishImportResult } from './parish-need.model';
import { ToastService } from '../../core/notifications/toast.service';
import { serverMessage } from '../../shared/http-error';

@Component({
  selector: 'app-parishes-list',
  standalone: true,
  imports: [ExportButtonComponent, HasPermissionDirective, FormsModule, RouterLink],
  templateUrl: './parishes-list.component.html',
  styleUrl: './parishes-list.component.scss'
})
export class ParishesListComponent implements OnInit {
  readonly parishes = signal<Parish[]>([]);
  readonly query = signal('');
  readonly isFormOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  newParish: ParishFormValue = { name: '', city: '', pastorPersonId: null };
  /** Osoby z funkcją Proboszcz – do wyboru w formularzu. */
  /** Import spisu parafii z proboszczami (np. ze strony diecezji). */
  readonly importOpen = signal(false);
  readonly importFile = signal<File | null>(null);
  readonly importPreview = signal<ParishImportResult | null>(null);
  readonly importBusy = signal(false);
  readonly importError = signal<string | null>(null);
  readonly pastors = signal<{ id: string; fullName: string; parishName: string | null }[]>([]);

  constructor(
    private readonly parishesService: ParishesService,
    private readonly toast: ToastService,
    private readonly peopleService: PeopleService
  ) {}

  private loadPastors(): void {
    this.peopleService.search('', 1, 200, 'Pastor').subscribe({
      next: result => this.pastors.set(result.items.map(p => ({
        id: p.id,
        fullName: p.fullName,
        parishName: p.functions?.find(f => f.type === 'Pastor')?.parishName ?? null
      }))),
      error: () => this.pastors.set([])
    });
  }

  ngOnInit(): void {
    this.load();
  }

  readonly filteredParishes = () => {
    const term = this.query().trim().toLowerCase();
    if (!term) return this.parishes();
    return this.parishes().filter(
      p => p.name.toLowerCase().includes(term) || (p.city ?? '').toLowerCase().includes(term)
        || (p.deanery ?? '').toLowerCase().includes(term) || (p.pastorFullName ?? '').toLowerCase().includes(term)
    );
  };

  load(): void {
    this.parishesService.list().subscribe({
      next: parishes => this.parishes.set(parishes),
      error: () => this.toast.error('Nie udało się wczytać listy parafii.')
    });
  }

  openAddForm(): void {
    this.editingId.set(null);
    this.newParish = { name: '', city: '', pastorPersonId: null };
    this.isFormOpen.set(true);
    this.loadPastors();
  }

  openEditForm(parish: Parish): void {
    this.editingId.set(parish.id);
    this.newParish = {
      name: parish.name, city: parish.city ?? '', pastorPersonId: parish.pastorPersonId ?? null,
      deanery: parish.deanery ?? undefined, address: parish.address ?? undefined, phone: parish.phone ?? undefined,
      email: parish.email ?? undefined, website: parish.website ?? undefined
    };
    this.isFormOpen.set(true);
    this.loadPastors();
  }

  saveParish(): void {
    if (!this.newParish.name.trim()) {
      this.toast.error('Podaj nazwę parafii.');
      return;
    }
    const id = this.editingId();
    const request$ = id ? this.parishesService.update(id, this.newParish) : this.parishesService.create(this.newParish);
    request$.subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success(id ? 'Zapisano zmiany.' : 'Dodano parafię.');
        this.load();
      },
      error: err => this.toast.error(serverMessage(err, id ? 'Nie udało się zapisać zmian.' : 'Nie udało się dodać parafii.'))
    });
  }

  openImport(): void {
    this.importFile.set(null);
    this.importPreview.set(null);
    this.importError.set(null);
    this.importOpen.set(true);
  }

  closeImport(): void {
    this.importOpen.set(false);
  }

  onImportFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    input.value = '';
    if (!file) return;
    this.importFile.set(file);
    this.runImport(false);
  }

  /** Podgląd (apply = false) albo zapis importu. */
  runImport(apply: boolean): void {
    const file = this.importFile();
    if (!file || this.importBusy()) return;
    this.importBusy.set(true);
    this.importError.set(null);
    this.parishesService.importPastors(file, apply).subscribe({
      next: result => {
        this.importBusy.set(false);
        if (apply) {
          this.importOpen.set(false);
          this.toast.success(`Zaimportowano: ${result.newParishes} nowych parafii, ${result.newPeople} nowych osób, ` +
            `${result.pastorsSet + result.pastorsReplaced} przypisań proboszcza.`);
          this.load();
        } else {
          this.importPreview.set(result);
        }
      },
      error: err => {
        this.importBusy.set(false);
        this.importError.set(serverMessage(err, apply ? 'Import się nie udał.' : 'Nie udało się odczytać pliku.'));
      }
    });
  }

  readonly changeLabels: Record<string, string> = { set: 'przypisze', unchanged: 'bez zmian', replaced: 'zmieni' };

  cancel(): void {
    this.isFormOpen.set(false);
  }

  deleteParish(parish: Parish): void {
    if (!confirm(`Usunąć parafię „${parish.name}” (${parish.city})?`)) return;
    this.parishesService.delete(parish.id).subscribe({
      next: () => {
        this.toast.success('Parafia usunięta.');
        this.load();
      },
      error: err => this.toast.error(serverMessage(err, 'Nie udało się usunąć parafii.'))
    });
  }
}
