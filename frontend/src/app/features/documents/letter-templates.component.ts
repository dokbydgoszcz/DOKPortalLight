import { Component, OnInit, ViewChild, computed, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DocumentsService } from './documents.service';
import { DownloadedFile, INPUT_PREFIX, LetterField, LetterTemplate, SaveLetterTemplate, inputsOf, unknownFieldsOf } from './generated-document.model';
import { RichTextEditorComponent } from '../../shared/rich-text/rich-text-editor.component';
import { PersonPickerComponent } from '../../shared/person-picker/person-picker.component';
import { PdfPreviewComponent } from './pdf-preview.component';
import { ToastService } from '../../core/notifications/toast.service';
import { serverMessage } from '../../shared/http-error';
import { saveBlob } from '../../shared/attachments/attachment-rules';

const NEW_BODY =
  '<p style="text-align: right">{{pismo.miejscowość}}, dnia {{pismo.data słownie}}</p><p></p>' +
  '<h2 style="text-align: center">Tytuł pisma</h2><p></p><p>Treść pisma dla {{osoba.imię i nazwisko}}.</p>';

/** Lista wzorów pism i edytor treści z polami. */
@Component({
  selector: 'app-letter-templates',
  standalone: true,
  imports: [FormsModule, DatePipe, RichTextEditorComponent, PersonPickerComponent, PdfPreviewComponent],
  templateUrl: './letter-templates.component.html',
  styleUrl: './letter-templates.component.scss'
})
export class LetterTemplatesComponent implements OnInit {
  @ViewChild(RichTextEditorComponent) private editor?: RichTextEditorComponent;

  readonly templates = signal<LetterTemplate[]>([]);
  readonly fields = signal<LetterField[]>([]);
  readonly groups = computed(() => {
    const map = new Map<string, LetterField[]>();
    for (const f of this.fields()) map.set(f.group, [...(map.get(f.group) ?? []), f]);
    return [...map.entries()].map(([name, items]) => ({ name, items }));
  });
  readonly selectedId = signal<string | null>(null);
  readonly draft = signal<SaveLetterTemplate | null>(null);
  readonly dirty = signal(false);
  readonly saving = signal(false);
  readonly preview = signal<DownloadedFile | null>(null);
  previewPersonId = '';
  fieldChoice = '';

  readonly inputs = computed(() => inputsOf(this.draft()?.body ?? ''));
  readonly unknown = computed(() => unknownFieldsOf(this.draft()?.body ?? '', this.fields()));
  readonly selected = computed(() => this.templates().find(t => t.id === this.selectedId()) ?? null);

  constructor(private readonly documents: DocumentsService, private readonly toast: ToastService) {}

  ngOnInit(): void {
    this.documents.fields().subscribe({ next: f => this.fields.set(f), error: () => this.fields.set([]) });
    this.load();
  }

  load(selectId?: string): void {
    this.documents.templates(true).subscribe({
      next: items => {
        this.templates.set(items);
        const target = items.find(t => t.id === (selectId ?? this.selectedId())) ?? items[0];
        if (target) this.open(target, true); else this.clear();
      },
      error: () => this.toast.error('Nie udało się wczytać wzorów pism.')
    });
  }

  private confirmLeave(): boolean {
    return !this.dirty() || confirm('Masz niezapisane zmiany we wzorze. Porzucić je?');
  }

  private clear(): void {
    this.selectedId.set(null);
    this.draft.set(null);
    this.dirty.set(false);
  }

  open(template: LetterTemplate, force = false): void {
    if (!force && template.id !== this.selectedId() && !this.confirmLeave()) return;
    this.selectedId.set(template.id);
    this.draft.set({ name: template.name, body: template.body, useLetterhead: template.useLetterhead, isActive: template.isActive });
    this.dirty.set(false);
  }

  startNew(copyOf?: LetterTemplate): void {
    if (!this.confirmLeave()) return;
    this.selectedId.set(null);
    this.draft.set(copyOf
      ? { name: `${copyOf.name} (kopia)`, body: copyOf.body, useLetterhead: copyOf.useLetterhead, isActive: true }
      : { name: 'Nowy wzór', body: NEW_BODY, useLetterhead: true, isActive: true });
    this.dirty.set(true);
  }

  update(patch: Partial<SaveLetterTemplate>): void {
    const current = this.draft();
    if (!current) return;
    this.draft.set({ ...current, ...patch });
    this.dirty.set(true);
  }

  insertField(key: string): void {
    if (!key) return;
    this.editor?.insertText(`{{${key}}}`);
    this.fieldChoice = '';
  }

  insertInput(): void {
    const name = prompt('Nazwa pola do wpisania przy generowaniu (np. „Data chrztu”):')?.trim();
    if (!name) return;
    this.editor?.insertText(`{{${INPUT_PREFIX}${name.replace(/[{}]/g, '')}}}`);
  }

  save(): void {
    const draft = this.draft();
    if (!draft || this.saving()) return;
    if (!draft.name.trim()) {
      this.toast.error('Podaj nazwę wzoru.');
      return;
    }
    this.saving.set(true);
    const id = this.selectedId();
    const request$ = id ? this.documents.updateTemplate(id, draft) : this.documents.createTemplate(draft);
    request$.subscribe({
      next: saved => {
        this.saving.set(false);
        this.dirty.set(false);
        this.toast.success(id ? 'Zapisano wzór.' : 'Dodano wzór.');
        this.load(saved.id);
      },
      error: err => {
        this.saving.set(false);
        this.toast.error(serverMessage(err, 'Nie udało się zapisać wzoru.'));
      }
    });
  }

  remove(): void {
    const template = this.selected();
    if (!template) {
      this.clear();
      this.load();
      return;
    }
    if (!confirm(`Usunąć wzór „${template.name}”?`)) return;
    this.documents.deleteTemplate(template.id).subscribe({
      next: () => {
        this.toast.success('Usunięto wzór.');
        this.clear();
        this.load();
      },
      error: err => this.toast.error(serverMessage(err, 'Nie udało się usunąć wzoru.'))
    });
  }

  showPreview(format: 'pdf' | 'docx' = 'pdf'): void {
    const draft = this.draft();
    if (!draft) return;
    this.documents.preview(draft.body, draft.useLetterhead, this.previewPersonId || null, format).subscribe({
      next: file => (format === 'pdf' ? this.preview.set(file) : saveBlob(file.blob, file.fileName)),
      error: err => this.toast.error(serverMessage(err, 'Nie udało się przygotować podglądu.'))
    });
  }
}
