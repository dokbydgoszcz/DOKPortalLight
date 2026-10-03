import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ParishNeedsService } from './parish-needs.service';
import { ParishesService } from './parishes.service';
import { PeopleService } from '../people/people.service';
import { AssignedPerson, CreateParishNeedValue, Parish, ParishNeed } from './parish-need.model';
import { Person } from '../people/person.model';
import { ToastService } from '../../core/notifications/toast.service';
import { serverMessage } from '../../shared/http-error';

@Component({
  selector: 'app-parish-board',
  standalone: true,
  imports: [HasPermissionDirective, FormsModule],
  templateUrl: './parish-board.component.html',
  styleUrl: './parish-board.component.scss'
})
export class ParishBoardComponent implements OnInit {
  readonly needs = signal<ParishNeed[]>([]);
  readonly isAddFormOpen = signal(false);
  readonly editingNeedId = signal<string | null>(null);
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
    this.editingNeedId.set(null);
    this.newNeed = { parishId: '', description: '' };
    this.isAddFormOpen.set(true);
  }

  openEditForm(need: ParishNeed): void {
    this.editingNeedId.set(need.id);
    this.newNeed = { parishId: need.parishId, description: need.description };
    this.isAddFormOpen.set(true);
  }

  canSaveNeed(): boolean {
    return !!this.newNeed.parishId && !!this.newNeed.description.trim();
  }

  saveNeed(): void {
    const id = this.editingNeedId();
    const request$ = id ? this.parishNeedsService.update(id, this.newNeed) : this.parishNeedsService.create(this.newNeed);
    request$.subscribe({
      next: () => {
        this.isAddFormOpen.set(false);
        this.toast.success(id ? 'Zapisano zmiany.' : 'Dodano zapotrzebowanie.');
        this.load();
      },
      error: err => this.toast.error(id ? serverMessage(err, 'Nie udało się zapisać zmian.') : 'Nie udało się dodać zapotrzebowania.')
    });
  }

  /** Osoby, których jeszcze nie skierowano do zapotrzebowania z otwartego okna. */
  assignablePeople(): Person[] {
    const need = this.needs().find(n => n.id === this.assigningNeedId());
    const assigned = new Set((need?.assignedPeople ?? []).map(p => p.personId));
    return this.people.filter(p => !assigned.has(p.id));
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
      error: err => this.toast.error(serverMessage(err, 'Nie udało się skierować katechisty.'))
    });
  }

  unassign(need: ParishNeed, person: AssignedPerson): void {
    if (!confirm(`Odpiąć „${person.fullName}” od zapotrzebowania?`)) return;
    this.parishNeedsService.unassign(need.id, person.personId).subscribe({
      next: () => {
        this.toast.success('Odpięto osobę.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się odpiąć osoby.')
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
