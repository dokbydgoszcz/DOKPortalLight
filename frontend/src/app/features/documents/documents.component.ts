import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DocumentsService } from './documents.service';
import { DOCUMENT_TEMPLATE_LABELS, DocumentTemplate, GeneratedDocument, GenerateDocumentValue } from './generated-document.model';
import { PeopleService } from '../people/people.service';
import { Person } from '../people/person.model';

@Component({
  selector: 'app-documents',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './documents.component.html',
  styleUrl: './documents.component.scss'
})
export class DocumentsComponent implements OnInit {
  readonly history = signal<GeneratedDocument[]>([]);
  readonly templateLabels = DOCUMENT_TEMPLATE_LABELS;
  readonly templates = Object.keys(DOCUMENT_TEMPLATE_LABELS) as DocumentTemplate[];
  people: Person[] = [];
  form: GenerateDocumentValue = { template: 'LetterToBishop', personId: '', additionalNotes: '' };

  constructor(
    private readonly documentsService: DocumentsService,
    private readonly peopleService: PeopleService
  ) {}

  ngOnInit(): void {
    this.load();
    this.peopleService.search('', 1, 200).subscribe(result => (this.people = result.items));
  }

  load(): void {
    this.documentsService.history().subscribe(items => this.history.set(items));
  }

  generate(): void {
    this.documentsService.generate(this.form).subscribe(blob => {
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${this.form.template}.pdf`;
      link.click();
      window.URL.revokeObjectURL(url);
      this.load();
    });
  }
}
