import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { PeopleService } from './people.service';
import { PersonOverview } from './person-overview.model';
import { functionText } from './person.model';
import { roleLabel } from '../../shared/role-labels';
import { serverMessage } from '../../shared/http-error';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { ToastService } from '../../core/notifications/toast.service';
import { saveBlob } from '../../shared/attachments/attachment-rules';

const MAX_PHOTO_BYTES = 5 * 1024 * 1024;

/** Kartoteka osoby: zdjęcie i dane, a pod nimi wszystkie powiązania (sprawy, misje, kandydatura, pisma, superwizje, konto). */
/** Strony, z których można wejść na kartę osoby (parametr „z”), i powrót do nich. */
export const BACK_TARGETS: Record<string, { url: string; label: string }> = {
  osoby: { url: '/people', label: 'Wróć do listy osób' },
  uzytkownicy: { url: '/admin/users', label: 'Wróć do użytkowników i ról' },
  parafie: { url: '/parishes', label: 'Wróć do listy parafii' }
};

@Component({
  selector: 'app-person-card',
  standalone: true,
  imports: [RouterLink, DatePipe],
  templateUrl: './person-card.component.html',
  styleUrl: './person-card.component.scss'
})
export class PersonCardComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly peopleService = inject(PeopleService);
  private readonly title = inject(Title);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);

  readonly overview = signal<PersonOverview | null>(null);
  readonly error = signal<string | null>(null);
  readonly photoUrl = signal<string | null>(null);
  readonly uploading = signal(false);
  readonly functionText = functionText;
  readonly roleLabel = roleLabel;
  readonly canManage = this.auth.hasPermission(Permissions.PeopleManage);
  /** Kopia danych i anonimizacja (RODO) – tylko administrator kont. */
  readonly canGdpr = this.auth.hasPermission(Permissions.UsersManage);
  readonly gdprBusy = signal(false);
  /** Powrót tam, skąd przyszliśmy (parametr „z” w adresie); domyślnie lista osób. */
  readonly back = BACK_TARGETS[this.route.snapshot.queryParamMap?.get('z') ?? ''] ?? BACK_TARGETS['osoby'];

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.peopleService.overview(id).subscribe({
      next: overview => {
        this.overview.set(overview);
        this.title.setTitle(`${overview.person.fullName} | DOK Portal`);
        if (overview.person.photoUpdatedAtUtc) this.loadPhoto();
      },
      error: err => this.error.set(err?.status === 404 ? 'Nie znaleziono osoby.' : serverMessage(err, 'Nie udało się wczytać karty osoby.'))
    });
  }

  ngOnDestroy(): void {
    this.setPhoto(null);
  }

  private setPhoto(blob: Blob | null): void {
    const previous = this.photoUrl();
    if (previous) URL.revokeObjectURL(previous);
    this.photoUrl.set(blob ? URL.createObjectURL(blob) : null);
  }

  private loadPhoto(): void {
    const o = this.overview();
    if (!o) return;
    this.peopleService.photo(o.person.id).subscribe({ next: blob => this.setPhoto(blob), error: () => this.setPhoto(null) });
  }

  onPhotoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    const o = this.overview();
    if (!file || !o) return;
    if (!/^image\/(jpeg|png|webp)$/.test(file.type)) {
      this.toast.error('Zdjęcie musi być w formacie JPG, PNG lub WEBP.');
      return;
    }
    if (file.size > MAX_PHOTO_BYTES) {
      this.toast.error('Zdjęcie jest za duże – maksymalnie 5 MB.');
      return;
    }
    this.uploading.set(true);
    this.peopleService.uploadPhoto(o.person.id, file).subscribe({
      next: person => {
        this.uploading.set(false);
        this.overview.set({ ...o, person });
        this.setPhoto(file);
        this.toast.success('Zapisano zdjęcie.');
      },
      error: err => {
        this.uploading.set(false);
        this.toast.error(serverMessage(err, 'Nie udało się zapisać zdjęcia.'));
      }
    });
  }

  removePhoto(): void {
    const o = this.overview();
    if (!o || !confirm('Usunąć zdjęcie?')) return;
    this.peopleService.deletePhoto(o.person.id).subscribe({
      next: () => {
        this.setPhoto(null);
        this.overview.set({ ...o, person: { ...o.person, photoUpdatedAtUtc: null } });
        this.toast.success('Usunięto zdjęcie.');
      },
      error: err => this.toast.error(serverMessage(err, 'Nie udało się usunąć zdjęcia.'))
    });
  }

  exportData(): void {
    const o = this.overview();
    if (!o) return;
    this.gdprBusy.set(true);
    this.peopleService.gdprExport(o.person.id).subscribe({
      next: blob => {
        this.gdprBusy.set(false);
        saveBlob(blob, `dane-osobowe-${o.person.lastName}.json`);
      },
      error: err => {
        this.gdprBusy.set(false);
        this.toast.error(serverMessage(err, 'Nie udało się przygotować kopii danych.'));
      }
    });
  }

  anonymize(): void {
    const o = this.overview();
    if (!o) return;
    const typed = prompt(
      `Usunięcie danych (RODO) jest nieodwracalne: imię i nazwisko, kontakt, uwagi, notatki duszpasterskie, pliki, pisma i konto ` +
      `zostaną trwale usunięte, zostaną tylko anonimowe liczby do statystyk. Najpierw pobierz kopię danych, jeśli osoba o nią prosi.\n\n` +
      `Aby potwierdzić, wpisz nazwisko: ${o.person.lastName}`);
    if (typed === null) return;
    if (typed.trim().toLowerCase() !== o.person.lastName.trim().toLowerCase()) {
      this.toast.error('Nazwisko się nie zgadza – dane nie zostały usunięte.');
      return;
    }
    this.gdprBusy.set(true);
    this.peopleService.anonymize(o.person.id).subscribe({
      next: () => {
        this.gdprBusy.set(false);
        this.setPhoto(null);
        this.toast.success('Dane osoby zostały usunięte (zanonimizowane).');
        this.load();
      },
      error: err => {
        this.gdprBusy.set(false);
        this.toast.error(serverMessage(err, 'Nie udało się usunąć danych osoby.'));
      }
    });
  }

  /** „ul. Długa 5, 85-001 Bydgoszcz” albo null, gdy adresu nie ma. */
  address(p: { addressStreet?: string | null; addressPostalCode?: string | null; addressCity?: string | null }): string | null {
    const town = [p.addressPostalCode, p.addressCity].filter(Boolean).join(' ');
    return [p.addressStreet, town].filter(Boolean).join(', ') || null;
  }

  initials(fullName: string): string {
    return fullName.split(' ').filter(Boolean).slice(0, 2).map(p => p[0]).join('').toUpperCase();
  }

  /** Liczba powiązań – te same dane, które blokują „Usuń” w Osobach. */
  links(o: PersonOverview): number {
    return (o.dokCases?.length ?? 0) + (o.casesLed?.length ?? 0) + (o.candidate ? 1 : 0) + (o.missions?.length ?? 0)
      + (o.parishAssignments?.length ?? 0) + (o.formatorFunctions?.length ?? 0) + (o.documents?.length ?? 0) + (o.account ? 1 : 0);
  }
}
