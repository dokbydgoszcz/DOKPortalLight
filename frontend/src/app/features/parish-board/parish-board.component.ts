import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ParishNeedsService } from './parish-needs.service';
import { ParishesService } from './parishes.service';
import { PeopleService } from '../people/people.service';
import { CreateParishNeedValue, Parish, ParishNeed } from './parish-need.model';
import { Person } from '../people/person.model';
import { ToastService } from '../../core/notifications/toast.service';

@Component({
  selector: 'app-parish-board',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './parish-board.component.html',
  styleUrl: './parish-board.component.scss'
})
export class ParishBoardComponent implements OnInit {
  readonly needs = signal<ParishNeed[]>([]);
  readonly isAddFormOpen = signal(false);
  readonly assigningNeedId = signal<string | null>(null);
  parishes: Parish[] = [];
  people: Person[] = [];
  newNeed: CreateParishNeedValue = { parishId: '', description: '' };
  assignPersonId = '';

  constructor(
    private readonly parishNeedsService: ParishNeedsService,
    private readonly parishesService: ParishesService,
    private readonly peopleService: PeopleService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
    this.parishesService.list().subscribe({
      next: parishes => (this.parishes = parishes),
      error: () => this.toast.error('Nie udało się wczytać listy parafii.')
    });
    this.peopleService.search('', 1, 200).subscribe({
      next: result => (this.people = result.items),
      error: () => this.toast.error('Nie udało się wczytać listy osób.')
    });
  }

  load(): void {
    this.parishNeedsService.list().subscribe({
      next: needs => this.needs.set(needs),
      error: () => this.toast.error('Nie udało się wczytać zapotrzebowań parafii.')
    });
  }

  openAddForm(): void {
    this.newNeed = { parishId: '', description: '' };
    this.isAddFormOpen.set(true);
  }

  createNeed(): void {
    this.parishNeedsService.create(this.newNeed).subscribe({
      next: () => {
        this.isAddFormOpen.set(false);
        this.toast.success('Dodano zapotrzebowanie.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się dodać zapotrzebowania.')
    });
  }

  openAssignForm(need: ParishNeed): void {
    this.assignPersonId = '';
    this.assigningNeedId.set(need.id);
  }

  confirmAssign(): void {
    const id = this.assigningNeedId();
    if (!id) return;
    this.parishNeedsService.assign(id, this.assignPersonId).subscribe({
      next: () => {
        this.assigningNeedId.set(null);
        this.toast.success('Skierowano katechistę.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się skierować katechisty.')
    });
  }

  cancelAssign(): void {
    this.assigningNeedId.set(null);
  }

  deleteNeed(need: ParishNeed): void {
    if (!confirm(`Usunąć zapotrzebowanie „${need.description}”?`)) return;
    this.parishNeedsService.delete(need.id).subscribe({
      next: () => {
        this.toast.success('Zapotrzebowanie usunięte.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się usunąć zapotrzebowania.')
    });
  }
}
