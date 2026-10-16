import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DocumentsService } from './documents.service';
import { DownloadedFile, GeneratedDocument, LetterFormat, LetterTemplate } from './generated-document.model';
import { PersonPickerComponent } from '../../shared/person-picker/person-picker.component';
import { ToastService } from '../../core/notifications/toast.service';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { serverMessage } from '../../shared/http-error';
import { saveBlob } from '../../shared/attachments/attachment-rules';
import { PdfPreviewComponent } from './pdf-preview.component';
import { LetterTemplatesComponent } from './letter-templates.component';
import { LetterheadSettingsComponent } from './letterhead-settings.component';

type Tab = 'generate' | 'templates' | 'letterhead';

@Component({
  selector: 'app-documents',
  standalone: true,
  imports: [HasPermissionDirective, FormsModule, DatePipe, PersonPickerComponent, PdfPreviewComponent, LetterTemplatesComponent, LetterheadSettingsComponent],
  templateUrl: './documents.component.html',
  styleUrl: './documents.component.scss'
})
export class DocumentsComponent implements OnInit {
  readonly history = signal<GeneratedDocument[]>([]);
  readonly templates = signal<LetterTemplate[]>([]);
  readonly tab = signal<Tab>('generate');
  readonly busy = signal(false);
  /** Pola bez danych w ostatnio wygenerowanym piśmie. */
  readonly lastMissing = signal<string[]>([]);
  readonly preview = signal<DownloadedFile | null>(null);
  readonly canEditTemplates: boolean;
  readonly canGenerate: boolean;

  templateId = '';
  personId = '';
  additionalNotes = '';
  inputs: Record<string, string> = {};

  constructor(
    private readonly documentsService: DocumentsService,
    private readonly toast: ToastService,
    auth: AuthService
  ) {
    this.canEditTemplates = auth.hasPermission(Permissions.DocumentTemplatesManage);
    this.canGenerate = auth.hasPermission(Permissions.DocumentsGenerate);
  }

  ngOnInit(): void {
    this.load();
    if (this.canGenerate) this.loadTemplates();
  }

  load(): void {
    this.documentsService.history().subscribe(items => this.history.set(items));
  }

  loadTemplates(): void {
    this.documentsService.templates().subscribe({
      next: items => {
        this.templates.set(items);
        if (!items.some(t => t.id === this.templateId)) this.selectTemplate(items[0]?.id ?? '');
      },
      error: () => this.toast.error('Nie udało się wczytać wzorów pism.')
    });
  }

  switchTab(tab: Tab): void {
    this.tab.set(tab);
    if (tab === 'generate' && this.canGenerate) this.loadTemplates();
  }

  get selected(): LetterTemplate | undefined {
    return this.templates().find(t => t.id === this.templateId);
  }

  selectTemplate(id: string): void {
    this.templateId = id;
    const kept: Record<string, string> = {};
    for (const name of this.selected?.inputs ?? []) kept[name] = this.inputs[name] ?? '';
    this.inputs = kept;
    this.lastMissing.set([]);
  }

  private filledInputs(): Record<string, string> {
    return Object.fromEntries(Object.entries(this.inputs).filter(([, v]) => v.trim()).map(([k, v]) => [k, v.trim()]));
  }

  showPreview(): void {
    const template = this.selected;
    if (!template || !this.personId) return;
    this.busy.set(true);
    this.documentsService.preview(template.body, template.useLetterhead, this.personId, 'pdf', this.filledInputs()).subscribe({
      next: file => {
        this.busy.set(false);
        this.preview.set(file);
      },
      error: err => {
        this.busy.set(false);
        this.toast.error(serverMessage(err, 'Nie udało się przygotować podglądu.'));
      }
    });
  }

  generate(format: LetterFormat): void {
    if (!this.templateId || !this.personId || this.busy()) return;
    this.busy.set(true);
    this.documentsService.generate({
      letterTemplateId: this.templateId, personId: this.personId, additionalNotes: this.additionalNotes.trim() || undefined,
      inputs: this.filledInputs(), format
    }).subscribe({
      next: file => {
        this.busy.set(false);
        saveBlob(file.blob, file.fileName);
        this.lastMissing.set(file.missing);
        this.toast.success(file.missing.length ? 'Pismo gotowe – uzupełnij odręcznie brakujące dane (lista pod formularzem).' : 'Pismo gotowe.');
        this.load();
      },
      error: err => {
        this.busy.set(false);
        this.toast.error(serverMessage(err, 'Nie udało się wygenerować pisma.'));
      }
    });
  }

  download(doc: GeneratedDocument, format: LetterFormat = 'pdf'): void {
    this.documentsService.download(doc.id, format).subscribe({
      next: file => {
        saveBlob(file.blob, file.fileName);
        this.load();
      },
      error: err => this.toast.error(serverMessage(err, 'Nie udało się pobrać dokumentu.'))
    });
  }

  remove(doc: GeneratedDocument): void {
    if (!confirm(`Usunąć dokument „${doc.title}” dla ${doc.personFullName}?`)) return;
    this.documentsService.delete(doc.id).subscribe({
      next: () => {
        this.toast.success('Dokument usunięty.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się usunąć dokumentu.')
    });
  }
}
