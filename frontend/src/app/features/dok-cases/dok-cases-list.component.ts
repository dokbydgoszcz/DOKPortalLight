import { ExportButtonComponent } from '../../shared/export/export-button.component';
import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DokCasesService } from './dok-cases.service';
import { DokCase, DokCaseFormValue } from './dok-case.model';
import { DokCaseFormComponent } from './dok-case-form.component';
import { PastoralNotesService } from './pastoral-notes.service';
import { PastoralNote } from './pastoral-note.model';
import { CaseDocumentsService } from './case-documents.service';
import { CaseDocument } from './case-document.model';
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
  imports: [ExportButtonComponent, DokCaseFormComponent, PaginationComponent, FormsModule, DatePipe],
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

  readonly documentsCase = signal<DokCase | null>(null);
  readonly documents = signal<CaseDocument[]>([]);
  newDocumentName = '';

  constructor(
    private readonly dokCasesService: DokCasesService,
    private readonly pastoralNotesService: PastoralNotesService,
    private readonly caseDocumentsService: CaseDocumentsService,
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

  openDocuments(dokCase: DokCase): void {
    this.documentsCase.set(dokCase);
    this.newDocumentName = '';
    this.loadDocuments(dokCase.id);
  }

  closeDocuments(): void {
    this.documentsCase.set(null);
    this.documents.set([]);
  }

  private loadDocuments(caseId: string): void {
    this.caseDocumentsService.list(caseId).subscribe({
      next: documents => this.documents.set(documents),
      error: () => this.toast.error('Nie udało się wczytać listy dokumentów.')
    });
  }

  addDocument(): void {
    const dokCase = this.documentsCase();
    if (!dokCase || !this.newDocumentName.trim()) return;
    this.caseDocumentsService.create(dokCase.id, this.newDocumentName.trim()).subscribe({
      next: () => {
        this.newDocumentName = '';
        this.toast.success('Dodano pozycję na liście dokumentów.');
        this.loadDocuments(dokCase.id);
      },
      error: () => this.toast.error('Nie udało się dodać dokumentu.')
    });
  }

  onFileSelected(document: CaseDocument, event: Event): void {
    const dokCase = this.documentsCase();
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!dokCase || !file) return;
    this.caseDocumentsService.upload(dokCase.id, document.id, file).subscribe({
      next: () => {
        this.toast.success('Plik przesłany.');
        input.value = '';
        this.loadDocuments(dokCase.id);
      },
      error: () => this.toast.error('Nie udało się przesłać pliku.')
    });
  }

  downloadDocument(caseDocument: CaseDocument): void {
    const dokCase = this.documentsCase();
    if (!dokCase) return;
    this.caseDocumentsService.download(dokCase.id, caseDocument.id).subscribe({
      next: response => {
        const blob = response.body;
        if (!blob) return;
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = caseDocument.originalFileName ?? caseDocument.name;
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(url);
      },
      error: () => this.toast.error('Nie udało się pobrać pliku.')
    });
  }

  formatFileSize(bytes: number | null): string {
    if (bytes === null) return '';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
}
