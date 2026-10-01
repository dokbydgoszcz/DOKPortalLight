import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { UsersService } from './users.service';
import { ALL_ROLES, AppUserAccount, CreateUserValue } from './user.model';
import { PeopleService } from '../people/people.service';
import { Person } from '../people/person.model';

@Component({
  selector: 'app-users-list',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './users-list.component.html',
  styleUrl: './users-list.component.scss'
})
export class UsersListComponent implements OnInit {
  readonly users = signal<AppUserAccount[]>([]);
  readonly allRoles = ALL_ROLES;

  newUser: Omit<CreateUserValue, 'personId'> = { email: '', password: '', roles: [] };
  confirmPassword = '';
  readonly createError = signal<string | null>(null);

  personQuery = '';
  readonly personResults = signal<Person[]>([]);
  readonly selectedPerson = signal<Person | null>(null);
  readonly addingNewPerson = signal(false);
  newPersonFirstName = '';
  newPersonLastName = '';
  private personSearchTimer: ReturnType<typeof setTimeout> | null = null;

  readonly resettingUserId = signal<string | null>(null);
  resetPasswordValue = '';
  resetPasswordConfirm = '';
  readonly resetError = signal<string | null>(null);

  constructor(
    private readonly usersService: UsersService,
    private readonly peopleService: PeopleService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.usersService.list().subscribe(users => this.users.set(users));
  }

  onPersonQueryChange(): void {
    if (this.personSearchTimer) {
      clearTimeout(this.personSearchTimer);
    }
    const query = this.personQuery.trim();
    if (!query) {
      this.personResults.set([]);
      return;
    }
    this.personSearchTimer = setTimeout(() => {
      this.peopleService.search(query).subscribe(result => this.personResults.set(result.items));
    }, 300);
  }

  selectPerson(person: Person): void {
    this.selectedPerson.set(person);
    this.personQuery = '';
    this.personResults.set([]);
    this.addingNewPerson.set(false);
  }

  clearSelectedPerson(): void {
    this.selectedPerson.set(null);
  }

  startAddingNewPerson(): void {
    this.addingNewPerson.set(true);
    this.personResults.set([]);
    this.personQuery = '';
  }

  cancelAddingNewPerson(): void {
    this.addingNewPerson.set(false);
    this.newPersonFirstName = '';
    this.newPersonLastName = '';
  }

  createUser(): void {
    this.createError.set(null);

    if (this.newUser.password !== this.confirmPassword) {
      this.createError.set('Podane hasła różnią się od siebie.');
      return;
    }

    if (this.addingNewPerson()) {
      if (!this.newPersonFirstName.trim() || !this.newPersonLastName.trim()) {
        this.createError.set('Podaj imię i nazwisko nowej osoby.');
        return;
      }
      this.peopleService
        .create({ firstName: this.newPersonFirstName.trim(), lastName: this.newPersonLastName.trim() })
        .subscribe({
          next: person => this.submitCreateUser(person.id),
          error: () => this.createError.set('Nie udało się utworzyć nowej osoby.')
        });
      return;
    }

    this.submitCreateUser(this.selectedPerson()?.id);
  }

  private submitCreateUser(personId: string | undefined): void {
    const value: CreateUserValue = { ...this.newUser, personId };
    this.usersService.create(value).subscribe({
      next: () => {
        this.newUser = { email: '', password: '', roles: [] };
        this.confirmPassword = '';
        this.selectedPerson.set(null);
        this.addingNewPerson.set(false);
        this.newPersonFirstName = '';
        this.newPersonLastName = '';
        this.load();
      },
      error: () => this.createError.set('Nie udało się utworzyć konta użytkownika.')
    });
  }

  toggleRole(user: AppUserAccount, role: string, checked: boolean): void {
    const roles = checked ? [...user.roles, role] : user.roles.filter(r => r !== role);
    this.usersService.assignRoles(user.id, roles).subscribe(() => this.load());
  }

  startResetPassword(userId: string): void {
    this.resettingUserId.set(userId);
    this.resetPasswordValue = '';
    this.resetPasswordConfirm = '';
    this.resetError.set(null);
  }

  cancelResetPassword(): void {
    this.resettingUserId.set(null);
  }

  confirmResetPassword(userId: string): void {
    this.resetError.set(null);
    if (!this.resetPasswordValue || this.resetPasswordValue !== this.resetPasswordConfirm) {
      this.resetError.set('Podane hasła różnią się od siebie.');
      return;
    }
    this.usersService.resetPassword(userId, this.resetPasswordValue).subscribe({
      next: () => this.resettingUserId.set(null),
      error: () => this.resetError.set('Nie udało się zresetować hasła.')
    });
  }
}
