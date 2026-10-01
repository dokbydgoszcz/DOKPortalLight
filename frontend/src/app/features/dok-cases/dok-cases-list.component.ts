import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DokCasesService } from './dok-cases.service';
import { DokCase, DokCaseFormValue } from './dok-case.model';
import { DokCaseFormComponent } from './dok-case-form.component';
import { PastoralNotesService } from './pastoral-notes.service';
import { PastoralNote } from './pastoral-note.model';
import { ToastService } from '../../core/notifications/toast.service';
import { PaginationComponent } from '../../shared/pagination.component';

const PATH_LABELS: Record<string, string> = {
  BaptismCandidate: 'Chrzest',
  Confirmation: 'Bierzmowanie',
  Communion: 'Stół Pański',
  Conversion: 'Konwersja',
  ReturnToUnity: 'Powrót do Jedności'
};

const PAGE_SIZE = 20;

@Component({
  selector: 'app-dok-cases-list',
  standalone: true,
  imports: [DokCaseFormComponent, PaginationComponent, FormsModule, DatePipe],
  templateUrl: './dok-cases-list.component.html',
  styleUrl: './dok-cases-list.component.scss'
})
export class DokCasesListComponent implements OnInit {
  readonly cases = signal<DokCase[]>([]);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = PAGE_SIZE;
  readonly isFormOpen = signal(false);
  formValue: DokCaseFormValue = { personId: '', path: 'BaptismCandidate', stage: 'Application', catechistPersonId: '' };

  readonly baptismCount = signal(0);
  readonly confirmationCount = signal(0);
  readonly conversionCount = signal(0);
  readonly communionCount = signal(0);

  readonly notesCase = signal<DokCase | null>(null);
  readonly notes = signal<PastoralNote[]>([]);
  newNoteContent = '';

  constructor(
    private readonly dokCasesService: DokCasesService,
    private readonly pastoralNotesService: PastoralNotesService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
    this.loadStats();
  }

  load(): void {
    this.dokCasesService.search(undefined, this.page(), this.pageSize).subscribe({
      next: result => {
        this.cases.set(result.items);
        this.totalCount.set(result.totalCount);
      },
      error: () => this.toast.error('Nie udało się wczytać listy podopiecznych.')
    });
  }

  private loadStats(): void {
    this.dokCasesService.search(undefined, 1, 1000).subscribe(result => {
      const all = result.items;
      this.baptismCount.set(all.filter(c => c.path === 'BaptismCandidate').length);
      this.confirmationCount.set(all.filter(c => c.path === 'Confirmation').length);
      this.conversionCount.set(all.filter(c => c.path === 'Conversion' || c.path === 'ReturnToUnity').length);
      this.communionCount.set(all.filter(c => c.path === 'Communion').length);
    });
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }

  openAddForm(): void {
    this.formValue = { personId: '', path: 'BaptismCandidate', stage: 'Application', catechistPersonId: '' };
    this.isFormOpen.set(true);
  }

  onSave(value: DokCaseFormValue): void {
    this.dokCasesService.create(value).subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success('Dodano podopiecznego.');
        this.load();
        this.loadStats();
      },
      error: () => this.toast.error('Nie udało się dodać podopiecznego.')
    });
  }

  onCancel(): void {
    this.isFormOpen.set(false);
  }

  pathLabel(path: string): string {
    return PATH_LABELS[path] ?? path;
  }

  deleteCase(dokCase: DokCase): void {
    if (!confirm(`Usunąć podopiecznego „${dokCase.personFullName}”?`)) return;
    this.dokCasesService.delete(dokCase.id).subscribe({
      next: () => {
        this.toast.success('Podopieczny usunięty.');
        this.load();
        this.loadStats();
      },
      error: () => this.toast.error('Nie udało się usunąć podopiecznego.')
    });
  }

  openNotes(dokCase: DokCase): void {
    this.notesCase.set(dokCase);
    this.newNoteContent = '';
    this.loadNotes(dokCase.id);
  }

  closeNotes(): void {
    this.notesCase.set(null);
    this.notes.set([]);
  }

  private loadNotes(caseId: string): void {
    this.pastoralNotesService.list(caseId).subscribe({
      next: notes => this.notes.set(notes),
      error: () => this.toast.error('Nie udało się wczytać notatek.')
    });
  }

  addNote(): void {
    const dokCase = this.notesCase();
    if (!dokCase || !this.newNoteContent.trim()) return;
    this.pastoralNotesService.create(dokCase.id, this.newNoteContent.trim()).subscribe({
      next: () => {
        this.newNoteContent = '';
        this.toast.success('Dodano notatkę.');
        this.loadNotes(dokCase.id);
      },
      error: () => this.toast.error('Nie udało się dodać notatki — sprawdź, czy masz uprawnienia.')
    });
  }
}
