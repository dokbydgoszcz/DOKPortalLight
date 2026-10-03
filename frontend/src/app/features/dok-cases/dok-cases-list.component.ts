import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
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
import { AttachmentsComponent } from '../../shared/attachments/attachments.component';
import { AttachmentsService } from '../../shared/attachments/attachments.service';
import { ACCEPT_ATTRIBUTE, RULES_HINT, formatFileSize, saveBlob, validateFile } from '../../shared/attachments/attachment-rules';
import { serverMessage } from '../../shared/attachments/upload-summary';
import { environment } from '../../../environments/environment';

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
  imports: [HasPermissionDirective, ExportButtonComponent, DokCaseFormComponent, PaginationComponent, AttachmentsComponent, FormsModule, DatePipe],
  templateUrl: './dok-cases-list.component.html',
  styleUrl: './dok-cases-list.component.scss'
})
export class DokCasesListComponent implements OnInit {
  readonly cases = signal<DokCase[]>([]);

  attendanceLabel(dokCase: DokCase): string {
    if (dokCase.meetingsRecorded === 0) return '—';
    const percent = Math.round((dokCase.meetingsAttended * 100) / dokCase.meetingsRecorded);
    return `${dokCase.meetingsAttended}/${dokCase.meetingsRecorded} (${percent}%)`;
  }

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
  /** Pliki wybrane do nowej notatki; wysyłane zaraz po jej zapisaniu. */
  readonly newNoteFiles = signal<File[]>([]);

  readonly documentsCase = signal<DokCase | null>(null);
  readonly documents = signal<CaseDocument[]>([]);
  newDocumentName = '';

  readonly accept = ACCEPT_ATTRIBUTE;
  readonly rulesHint = RULES_HINT;
  readonly formatFileSize = formatFileSize;

  constructor(
    private readonly dokCasesService: DokCasesService,
    private readonly pastoralNotesService: PastoralNotesService,
    private readonly caseDocumentsService: CaseDocumentsService,
    private readonly attachmentsService: AttachmentsService,
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
    this.newNoteFiles.set([]);
    this.loadNotes(dokCase.id);
  }

  closeNotes(): void {
    this.notesCase.set(null);
    this.notes.set([]);
    this.newNoteFiles.set([]);
  }

  loadNotes(caseId: string): void {
    this.pastoralNotesService.list(caseId).subscribe({
      next: notes => this.notes.set(notes),
      error: () => this.toast.error('Nie udało się wczytać notatek.')
    });
  }

  noteAttachmentsUrl(caseId: string, noteId: string): string {
    return `${environment.apiBaseUrl}/api/dok-cases/${caseId}/notes/${noteId}/attachments`;
  }

  onNoteFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const accepted: File[] = [];
    for (const file of Array.from(input.files ?? [])) {
      const error = validateFile(file);
      if (error) this.toast.error(`${file.name}: ${error}`);
      else accepted.push(file);
    }
    this.newNoteFiles.update(files => [...files, ...accepted]);
    input.value = '';
  }

  removeNoteFile(index: number): void {
    this.newNoteFiles.update(files => files.filter((_, i) => i !== index));
  }

  addNote(): void {
    const dokCase = this.notesCase();
    const files = this.newNoteFiles();
    if (!dokCase || (!this.newNoteContent.trim() && files.length === 0)) return;

    // Notatka złożona wyłącznie z plików (np. skan z Kindle Scribe) dostaje opis z nazw plików.
    const content = this.newNoteContent.trim() || `Załączono: ${files.map(f => f.name).join(', ')}`;
    this.pastoralNotesService.create(dokCase.id, content).subscribe({
      next: note => {
        this.newNoteContent = '';
        this.newNoteFiles.set([]);
        this.toast.success('Dodano notatkę.');
        this.attachmentsService.uploadMany(this.noteAttachmentsUrl(dokCase.id, note.id), files).subscribe(summary => {
          summary.errors.forEach(message => this.toast.error(message));
          this.loadNotes(dokCase.id);
        });
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
    const invalid = validateFile(file);
    if (invalid) {
      this.toast.error(invalid);
      input.value = '';
      return;
    }
    this.caseDocumentsService.upload(dokCase.id, document.id, file).subscribe({
      next: () => {
        this.toast.success('Plik przesłany.');
        input.value = '';
        this.loadDocuments(dokCase.id);
      },
      error: err => this.toast.error(serverMessage(err, 'Nie udało się przesłać pliku.'))
    });
  }

  /** Każdy wybrany plik zakłada nową pozycję na liście (nazwa z nazwy pliku) i od razu się do niej wysyła. */
  onBulkFilesSelected(event: Event): void {
    const dokCase = this.documentsCase();
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    if (!dokCase || files.length === 0) return;

    this.caseDocumentsService.uploadAsNewPositions(dokCase.id, files).subscribe(summary => {
      input.value = '';
      summary.errors.forEach(message => this.toast.error(message));
      if (summary.uploaded > 0) {
        this.toast.success(summary.uploaded === 1 ? 'Przesłano dokument.' : `Przesłano dokumenty: ${summary.uploaded}.`);
      }
      this.loadDocuments(dokCase.id);
    });
  }

  downloadDocument(caseDocument: CaseDocument): void {
    const dokCase = this.documentsCase();
    if (!dokCase) return;
    this.caseDocumentsService.download(dokCase.id, caseDocument.id).subscribe({
      next: response => {
        const blob = response.body;
        if (!blob) return;
        saveBlob(blob, caseDocument.originalFileName ?? caseDocument.name);
      },
      error: () => this.toast.error('Nie udało się pobrać pliku.')
    });
  }
}
