import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ParishNeedsService } from './parish-needs.service';
import { ParishesService } from './parishes.service';
import { PeopleService } from '../people/people.service';
import { CreateParishNeedValue, Parish, ParishNeed } from './parish-need.model';
import { Person } from '../people/person.model';

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
    private readonly peopleService: PeopleService
  ) {}

  ngOnInit(): void {
    this.load();
    this.parishesService.list().subscribe(parishes => (this.parishes = parishes));
    this.peopleService.search('', 1, 200).subscribe(result => (this.people = result.items));
  }

  load(): void {
    this.parishNeedsService.list().subscribe(needs => this.needs.set(needs));
  }

  openAddForm(): void {
    this.newNeed = { parishId: '', description: '' };
    this.isAddFormOpen.set(true);
  }

  createNeed(): void {
    this.parishNeedsService.create(this.newNeed).subscribe(() => {
      this.isAddFormOpen.set(false);
      this.load();
    });
  }

  openAssignForm(need: ParishNeed): void {
    this.assignPersonId = '';
    this.assigningNeedId.set(need.id);
  }

  confirmAssign(): void {
    const id = this.assigningNeedId();
    if (!id) return;
    this.parishNeedsService.assign(id, this.assignPersonId).subscribe(() => {
      this.assigningNeedId.set(null);
      this.load();
    });
  }

  cancelAssign(): void {
    this.assigningNeedId.set(null);
  }
}
