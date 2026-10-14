import { Component, OnInit, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { UsersService } from './users.service';
import { AppUserAccount, CreateUserValue } from './user.model';
import { PeopleService } from '../people/people.service';
import { Person } from '../people/person.model';
import { ToastService } from '../../core/notifications/toast.service';
import { serverMessage } from '../../shared/http-error';
import { ROLE_LABELS } from '../../shared/role-labels';


@Component({
  selector: 'app-users-list',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './users-list.component.html',
  styleUrl: './users-list.component.scss'
})
export class UsersListComponent implements OnInit {
  readonly users = signal<AppUserAccount[]>([]);
  readonly allRoles = signal<string[]>([]);

  /** Czytelne nazwy ról systemowych; role własne pokazujemy tak, jak je nazwano. */
  roleLabel(role: string): string {
    return ROLE_LABELS[role] ?? role;
  }

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

  /** Powiązanie istniejącego konta z osobą (wiersz w tabeli rozwija wyszukiwarkę osób). */
  readonly linkingUserId = signal<string | null>(null);
  linkQuery = '';
  readonly linkResults = signal<Person[]>([]);
  readonly linkError = signal<string | null>(null);
  private linkSearchTimer: ReturnType<typeof setTimeout> | null = null;

  readonly resettingUserId = signal<string | null>(null);
  resetPasswordValue = '';
  resetPasswordConfirm = '';
  readonly resetError = signal<string | null>(null);

  constructor(
    private readonly usersService: UsersService,
    private readonly peopleService: PeopleService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.usersService.list().subscribe({
      next: users => this.users.set(users),
      error: () => this.toast.error('Nie udało się wczytać listy użytkowników.')
    });
    this.usersService.listRoles().subscribe({
      next: roles => this.allRoles.set(roles),
      error: () => this.toast.error('Nie udało się wczytać listy ról.')
    });
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
      this.peopleService.search(query).subscribe({
        next: result => this.personResults.set(result.items),
        error: () => this.toast.error('Nie udało się wyszukać osób.')
      });
    }, 300);
  }

  selectPerson(person: Person): void {
    this.selectedPerson.set(person);
    // Login konta to e-mail osoby – nie wpisuje się go drugi raz.
    if (person.email) this.newUser.email = person.email;
    this.personQuery = '';
    this.personResults.set([]);
    this.addingNewPerson.set(false);
  }

  /** Login wynika z e-maila wybranej osoby (pole jest wtedy zablokowane). */
  loginFromPerson(): boolean {
    return !!this.selectedPerson()?.email;
  }

  clearSelectedPerson(): void {
    if (this.loginFromPerson()) this.newUser.email = '';
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

    const personEmail = this.selectedPerson()?.email;
    if (personEmail) this.newUser.email = personEmail;
    if (!this.newUser.email.trim()) {
      this.createError.set('Podaj e-mail – będzie loginem konta.');
      return;
    }

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
        // E-mail konta jest też adresem kontaktowym nowej osoby (jedna osoba = jedno konto).
        .create({ firstName: this.newPersonFirstName.trim(), lastName: this.newPersonLastName.trim(), email: this.newUser.email.trim() || undefined })
        .subscribe({
          next: person => this.submitCreateUser(person.id),
          error: err => this.createError.set(serverMessage(err, 'Nie udało się utworzyć nowej osoby.'))
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
        this.toast.success('Konto użytkownika utworzone.');
        this.load();
      },
      error: err => this.createError.set(serverMessage(err, 'Nie udało się utworzyć konta użytkownika.'))
    });
  }

  toggleRole(user: AppUserAccount, role: string, checked: boolean): void {
    const roles = checked ? [...user.roles, role] : user.roles.filter(r => r !== role);
    this.usersService.assignRoles(user.id, roles).subscribe({
      next: () => this.load(),
      error: err => {
        this.toast.error(serverMessage(err, 'Nie udało się zaktualizować ról.'));
        this.load();
      }
    });
  }

  /** Wyłączone konto nie zaloguje się, a jego otwarte sesje wygasają; można je włączyć z powrotem. */
  /** Edycja loginu (e-maila) konta w wierszu tabeli. */
  readonly editingLoginId = signal<string | null>(null);
  loginValue = '';

  startLoginEdit(user: AppUserAccount): void {
    this.loginValue = user.email;
    this.editingLoginId.set(user.id);
  }

  saveLogin(user: AppUserAccount): void {
    const email = this.loginValue.trim();
    if (!email || email === user.email) {
      this.editingLoginId.set(null);
      return;
    }
    this.usersService.changeLogin(user.id, email).subscribe({
      next: () => {
        this.editingLoginId.set(null);
        this.toast.success(user.personId
          ? `Login zmieniony na ${email} (e-mail osoby też).`
          : `Login zmieniony na ${email}.`);
        this.load();
      },
      error: err => this.toast.error(serverMessage(err, 'Nie udało się zmienić loginu.'))
    });
  }

  resetMfa(user: AppUserAccount): void {
    if (!confirm(`Wyłączyć weryfikację dwuetapową dla ${user.email}? Użytkownik zaloguje się samym hasłem i może ją potem włączyć od nowa (np. na nowym telefonie).`)) return;
    this.usersService.resetMfa(user.id).subscribe({
      next: () => {
        this.toast.success('Weryfikacja dwuetapowa wyłączona.');
        this.load();
      },
      error: err => this.toast.error(serverMessage(err, 'Nie udało się wyłączyć weryfikacji dwuetapowej.'))
    });
  }

  setActive(user: AppUserAccount, isActive: boolean): void {
    if (!isActive && !confirm(`Wyłączyć konto ${user.email}? Osoba nie zaloguje się, a jej otwarte sesje wygasną.`)) return;
    this.usersService.setActive(user.id, isActive).subscribe({
      next: () => {
        this.toast.success(isActive ? 'Konto włączone.' : 'Konto wyłączone.');
        this.load();
      },
      error: err => this.toast.error(serverMessage(err, 'Nie udało się zmienić stanu konta.'))
    });
  }

  startLinking(userId: string): void {
    this.linkingUserId.set(userId);
    this.linkQuery = '';
    this.linkResults.set([]);
    this.linkError.set(null);
  }

  cancelLinking(): void {
    this.linkingUserId.set(null);
  }

  onLinkQueryChange(): void {
    if (this.linkSearchTimer) {
      clearTimeout(this.linkSearchTimer);
    }
    const query = this.linkQuery.trim();
    if (!query) {
      this.linkResults.set([]);
      return;
    }
    this.linkSearchTimer = setTimeout(() => {
      this.peopleService.search(query).subscribe({
        next: result => this.linkResults.set(result.items),
        error: () => this.toast.error('Nie udało się wyszukać osób.')
      });
    }, 300);
  }

  linkPerson(user: AppUserAccount, person: Person): void {
    this.linkError.set(null);
    this.usersService.setPerson(user.id, person.id).subscribe({
      next: () => {
        this.linkingUserId.set(null);
        this.toast.success('Konto powiązane z osobą.');
        this.load();
      },
      error: (error: HttpErrorResponse) =>
        this.linkError.set(error.error?.title ?? 'Nie udało się powiązać konta z osobą.')
    });
  }

  unlinkPerson(user: AppUserAccount): void {
    if (!confirm(`Odpiąć konto „${user.email}” od osoby ${user.personFullName}?`)) return;
    this.usersService.setPerson(user.id, null).subscribe({
      next: () => {
        this.toast.success('Konto odpięte od osoby.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się odpiąć konta od osoby.')
    });
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
      next: () => {
        this.resettingUserId.set(null);
        this.toast.success('Hasło zresetowane.');
      },
      error: () => this.resetError.set('Nie udało się zresetować hasła.')
    });
  }
}
