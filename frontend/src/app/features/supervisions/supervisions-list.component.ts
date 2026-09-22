import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SupervisionsService } from './supervisions.service';
import { CreateSupervisionValue, Supervision } from './supervision.model';

@Component({
  selector: 'app-supervisions-list',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './supervisions-list.component.html',
  styleUrl: './supervisions-list.component.scss'
})
export class SupervisionsListComponent implements OnInit {
  readonly supervisions = signal<Supervision[]>([]);
  readonly isFormOpen = signal(false);
  newSupervision: CreateSupervisionValue = { institution: 'DOK', groupLabel: '', supervisionDate: '' };

  constructor(private readonly supervisionsService: SupervisionsService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.supervisionsService.list().subscribe(supervisions => this.supervisions.set(supervisions));
  }

  openAddForm(): void {
    this.newSupervision = { institution: 'DOK', groupLabel: '', supervisionDate: '' };
    this.isFormOpen.set(true);
  }

  createSupervision(): void {
    this.supervisionsService.create(this.newSupervision).subscribe(() => {
      this.isFormOpen.set(false);
      this.load();
    });
  }

  cancel(): void {
    this.isFormOpen.set(false);
  }
}
