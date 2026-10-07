import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DocumentsService } from './documents.service';
import { DOCUMENT_TEMPLATE_LABELS, GeneratedDocument, GenerateDocumentValue, OFFERED_TEMPLATES } from './generated-document.model';
import { PeopleService } from '../people/people.service';
import { Person } from '../people/person.model';
import { ToastService } from '../../core/notifications/toast.service';

@Component({
  selector: 'app-documents',
  standalone: true,
  imports: [HasPermissionDirective, FormsModule, DatePipe],
  templateUrl: './documents.component.html',
  styleUrl: './documents.component.scss'
})
export class DocumentsComponent implements OnInit {
  readonly history = signal<GeneratedDocument[]>([]);
  readonly templateLabels = DOCUMENT_TEMPLATE_LABELS;
  readonly templates = OFFERED_TEMPLATES;
  people: Person[] = [];
  form: GenerateDocumentValue = { template: 'LetterToBishop', personId: '', additionalNotes: '' };

  constructor(
    private readonly documentsService: DocumentsService,
    private readonly peopleService: PeopleService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
    this.peopleService.search('', 1, 200).subscribe(result => (this.people = result.items));
  }

  load(): void {
    this.documentsService.history().subscribe(items => this.history.set(items));
  }

  generate(): void {
    this.documentsService.generate(this.form).subscribe({
      next: blob => {
        this.saveBlob(blob, `${this.form.template}.pdf`);
        this.load();
      },
      error: () => this.toast.error('Nie udało się wygenerować dokumentu.')
    });
  }

  download(doc: GeneratedDocument): void {
    this.documentsService.download(doc.id).subscribe({
      next: blob => {
        this.saveBlob(blob, `${doc.template}.pdf`);
        this.load();
      },
      error: () => this.toast.error('Nie udało się pobrać dokumentu.')
    });
  }

  remove(doc: GeneratedDocument): void {
    if (!confirm(`Usunąć dokument „${this.templateLabels[doc.template]}” dla ${doc.personFullName}?`)) return;
    this.documentsService.delete(doc.id).subscribe({
      next: () => {
        this.toast.success('Dokument usunięty.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się usunąć dokumentu.')
    });
  }

  private saveBlob(blob: Blob, fileName: string): void {
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    window.URL.revokeObjectURL(url);
  }
}
