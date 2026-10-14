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

const MAX_PHOTO_BYTES = 5 * 1024 * 1024;

/** Kartoteka osoby: zdjęcie i dane, a pod nimi wszystkie powiązania (sprawy, misje, kandydatura, pisma, superwizje, konto). */
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

  ngOnInit(): void {
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

  initials(fullName: string): string {
    return fullName.split(' ').filter(Boolean).slice(0, 2).map(p => p[0]).join('').toUpperCase();
  }

  /** Liczba powiązań – te same dane, które blokują „Usuń” w Osobach. */
  links(o: PersonOverview): number {
    return (o.dokCases?.length ?? 0) + (o.casesLed?.length ?? 0) + (o.candidate ? 1 : 0) + (o.missions?.length ?? 0)
      + (o.parishAssignments?.length ?? 0) + (o.formatorFunctions?.length ?? 0) + (o.documents?.length ?? 0) + (o.account ? 1 : 0);
  }
}
