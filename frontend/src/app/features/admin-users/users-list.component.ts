import { Component, OnInit, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
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
        this.isCreateOpen.set(false);
        this.toast.success('Konto użytkownika utworzone.');
        this.load();
      },
      error: err => this.createError.set(serverMessage(err, 'Nie udało się utworzyć konta użytkownika.'))
    });
  }

  // ---------- Nowe konto (okno) ----------

  readonly isCreateOpen = signal(false);

  openCreate(): void {
    this.newUser = { email: '', password: '', roles: [] };
    this.confirmPassword = '';
    this.createError.set(null);
    this.selectedPerson.set(null);
    this.addingNewPerson.set(false);
    this.personQuery = '';
    this.personResults.set([]);
    this.isCreateOpen.set(true);
  }

  closeCreate(): void {
    this.isCreateOpen.set(false);
  }

  toggleNewRole(role: string, checked: boolean): void {
    this.newUser.roles = checked ? [...this.newUser.roles, role] : this.newUser.roles.filter(r => r !== role);
  }

  // ---------- Edycja konta (okno) ----------

  /** Konto otwarte w oknie edycji. */
  readonly editing = signal<AppUserAccount | null>(null);
  editLogin = '';
  editRoles: string[] = [];
  /** Osoba wybrana w oknie (null = bez osoby). */
  readonly editPerson = signal<{ id: string; fullName: string } | null>(null);
  readonly isPickingPerson = signal(false);
  linkQuery = '';
  readonly linkResults = signal<Person[]>([]);
  private linkSearchTimer: ReturnType<typeof setTimeout> | null = null;
  readonly editError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly isResetOpen = signal(false);
  resetPasswordValue = '';
  resetPasswordConfirm = '';

  openEdit(user: AppUserAccount): void {
    this.editing.set(user);
    this.editLogin = user.email;
    this.editRoles = [...user.roles];
    this.editPerson.set(user.personId ? { id: user.personId, fullName: user.personFullName ?? '' } : null);
    this.isPickingPerson.set(false);
    this.linkQuery = '';
    this.linkResults.set([]);
    this.editError.set(null);
    this.isResetOpen.set(false);
  }

  closeEdit(): void {
    this.editing.set(null);
  }

  hasEditRole(role: string): boolean {
    return this.editRoles.includes(role);
  }

  toggleEditRole(role: string, checked: boolean): void {
    this.editRoles = checked ? [...this.editRoles, role] : this.editRoles.filter(r => r !== role);
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

  pickPerson(person: Person): void {
    this.editPerson.set({ id: person.id, fullName: person.fullName });
    this.isPickingPerson.set(false);
    this.linkQuery = '';
    this.linkResults.set([]);
  }

  /** Zapisuje zmienione pola po kolei: login, osoba, role. */
  async saveEdit(): Promise<void> {
    const user = this.editing();
    if (!user || this.saving()) return;
    const login = this.editLogin.trim();
    if (!login) {
      this.editError.set('Podaj login (adres e-mail).');
      return;
    }
    this.editError.set(null);
    this.saving.set(true);
    try {
      if (login !== user.email) await firstValueFrom(this.usersService.changeLogin(user.id, login));
      const personId = this.editPerson()?.id ?? null;
      if (personId !== user.personId) await firstValueFrom(this.usersService.setPerson(user.id, personId));
      const sameRoles = this.editRoles.length === user.roles.length && this.editRoles.every(r => user.roles.includes(r));
      if (!sameRoles) await firstValueFrom(this.usersService.assignRoles(user.id, this.editRoles));
      this.editing.set(null);
      this.toast.success('Zapisano zmiany konta.');
    } catch (err) {
      this.editError.set(serverMessage(err, 'Nie udało się zapisać zmian konta.'));
    } finally {
      this.saving.set(false);
      this.load();
    }
  }

  openReset(): void {
    this.resetPasswordValue = '';
    this.resetPasswordConfirm = '';
    this.editError.set(null);
    this.isResetOpen.set(true);
  }

  confirmResetPassword(): void {
    const user = this.editing();
    if (!user) return;
    if (!this.resetPasswordValue || this.resetPasswordValue !== this.resetPasswordConfirm) {
      this.editError.set('Podane hasła różnią się od siebie.');
      return;
    }
    this.usersService.resetPassword(user.id, this.resetPasswordValue).subscribe({
      next: () => {
        this.isResetOpen.set(false);
        this.editError.set(null);
        this.toast.success('Hasło zresetowane – przy logowaniu użytkownik ustawi własne.');
      },
      error: err => this.editError.set(serverMessage(err, 'Nie udało się zresetować hasła.'))
    });
  }

  resetMfa(): void {
    const user = this.editing();
    if (!user || !confirm(`Wyłączyć weryfikację dwuetapową dla ${user.email}? Użytkownik zaloguje się samym hasłem i może ją potem włączyć od nowa (np. na nowym telefonie).`)) return;
    this.usersService.resetMfa(user.id).subscribe({
      next: updated => {
        this.editing.set({ ...user, mfaEnabled: updated?.mfaEnabled ?? false });
        this.toast.success('Weryfikacja dwuetapowa wyłączona.');
        this.load();
      },
      error: err => this.editError.set(serverMessage(err, 'Nie udało się wyłączyć weryfikacji dwuetapowej.'))
    });
  }

  /** Wyłączone konto nie zaloguje się, a jego otwarte sesje wygasają; można je włączyć z powrotem. */
  setActive(isActive: boolean): void {
    const user = this.editing();
    if (!user) return;
    if (!isActive && !confirm(`Wyłączyć konto ${user.email}? Osoba nie zaloguje się, a jej otwarte sesje wygasną.`)) return;
    this.usersService.setActive(user.id, isActive).subscribe({
      next: () => {
        this.editing.set({ ...user, isActive });
        this.toast.success(isActive ? 'Konto włączone.' : 'Konto wyłączone.');
        this.load();
      },
      error: err => this.editError.set(serverMessage(err, 'Nie udało się zmienić stanu konta.'))
    });
  }
}
