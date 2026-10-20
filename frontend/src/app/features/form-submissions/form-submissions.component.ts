import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ToastService } from '../../core/notifications/toast.service';
import { serverMessage } from '../../shared/http-error';
import { ImageShrinker } from '../../shared/image-shrink';
import { PersonPickerComponent } from '../../shared/person-picker/person-picker.component';
import { PeopleService } from '../people/people.service';
import { FUNCTION_LABELS, FunctionType, MONTH_NAMES, MONTH_NAMES_GENITIVE } from '../people/person.model';
import { FormSubmission, FormSubmissionCandidate, FormSubmissionFile, FormSubmissionStatus, PersonDraft } from './form-submission.model';
import { FormSubmissionsService } from './form-submissions.service';

/** Pola, które przenosimy ze zgłoszenia do osoby (edytowalne przed zapisem). */
interface EditValue {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  addressStreet: string;
  addressPostalCode: string;
  addressCity: string;
  nameDayDay: number | null;
  nameDayMonth: number | null;
}

type TextField = Exclude<keyof EditValue, 'nameDayDay' | 'nameDayMonth'>;

/** Wybór, dokąd trafią dane: nowa osoba, jedna z podpowiedzianych albo dowolna inna. */
export const NEW_PERSON = 'new';
export const OTHER_PERSON = 'other';

/**
 * Zgłoszenia z formularzy (Tally): przyjmowane automatycznie przez webhook, ale do osób trafiają
 * dopiero po przejrzeniu – tu poprawiasz dane, wybierasz osobę do aktualizacji albo dopisujesz nową.
 */
@Component({
  selector: 'app-form-submissions',
  standalone: true,
  imports: [FormsModule, DatePipe, RouterLink, PersonPickerComponent],
  templateUrl: './form-submissions.component.html',
  styleUrl: './form-submissions.component.scss'
})
export class FormSubmissionsComponent implements OnInit, OnDestroy {
  private readonly service = inject(FormSubmissionsService);
  private readonly people = inject(PeopleService);
  private readonly shrinker = inject(ImageShrinker);
  private readonly toast = inject(ToastService);

  readonly NEW = NEW_PERSON;
  readonly OTHER = OTHER_PERSON;
  readonly months = MONTH_NAMES;
  readonly monthsGenitive = MONTH_NAMES_GENITIVE;
  readonly textFields: ReadonlyArray<{ key: TextField; label: string; wide?: boolean }> = [
    { key: 'firstName', label: 'Imię' },
    { key: 'lastName', label: 'Nazwisko' },
    { key: 'email', label: 'E-mail' },
    { key: 'phone', label: 'Telefon' },
    { key: 'addressStreet', label: 'Ulica i numer', wide: true },
    { key: 'addressPostalCode', label: 'Kod pocztowy' },
    { key: 'addressCity', label: 'Miejscowość' }
  ];

  readonly tab = signal<FormSubmissionStatus>('Pending');
  readonly items = signal<FormSubmission[] | null>(null);
  readonly loadError = signal<string | null>(null);
  readonly pendingCount = signal<number | null>(null);

  readonly current = signal<FormSubmission | null>(null);
  readonly target = signal<string>(NEW_PERSON);
  readonly otherPersonId = signal('');
  readonly setPhoto = signal(false);
  readonly photoUrl = signal<string | null>(null);
  readonly photoError = signal<string | null>(null);
  readonly saving = signal(false);
  value: EditValue = this.emptyValue();
  private photoBlob: Blob | null = null;

  readonly candidate = computed(() => this.current()?.candidates.find(c => c.personId === this.target()) ?? null);
  readonly hasPhoto = computed(() => this.current()?.draft.photoIndex != null);

  ngOnInit(): void {
    this.load();
    this.refreshCount();
  }

  ngOnDestroy(): void {
    this.releasePhoto();
  }

  selectTab(tab: FormSubmissionStatus): void {
    if (tab === this.tab()) return;
    this.tab.set(tab);
    this.load();
  }

  load(): void {
    this.items.set(null);
    this.loadError.set(null);
    this.service.list(this.tab()).subscribe({
      next: list => this.items.set(list),
      error: err => this.loadError.set(serverMessage(err, 'Nie udało się wczytać zgłoszeń.'))
    });
  }

  private refreshCount(): void {
    this.service.pendingCount().subscribe({ next: count => this.pendingCount.set(count), error: () => this.pendingCount.set(null) });
  }

  displayName(s: FormSubmission): string {
    const name = [s.draft.firstName, s.draft.lastName].filter(Boolean).join(' ');
    return name || s.draft.email || 'Bez imienia i nazwiska';
  }

  suggestion(s: FormSubmission): string {
    const best = this.defaultCandidate(s);
    if (best) return `aktualizacja: ${best.fullName} (${best.matchedBy})`;
    return s.candidates.length ? `nowa osoba? podobne: ${s.candidates.map(c => c.fullName).join(', ')}` : 'nowa osoba';
  }

  nameDay(day: number | null, month: number | null): string {
    return day && month ? `${day} ${this.monthsGenitive[month - 1]}` : '—';
  }

  functionLabels(c: FormSubmissionCandidate): string {
    return c.functions.map(f => FUNCTION_LABELS[f as FunctionType] ?? f).join(', ');
  }

  /** Kandydat przyjmowany domyślnie: pasujący e-mailem albo imieniem i nazwiskiem (sam telefon to za mało). */
  private defaultCandidate(s: FormSubmission): FormSubmissionCandidate | null {
    return s.candidates.find(c => c.matchedBy.includes('e-mail') || c.matchedBy.includes('imię i nazwisko')) ?? null;
  }

  open(s: FormSubmission): void {
    this.releasePhoto();
    this.current.set(s);
    this.value = this.fromDraft(s.draft);
    this.otherPersonId.set('');
    this.chooseTarget(this.defaultCandidate(s)?.personId ?? NEW_PERSON);
    if (s.draft.photoIndex != null) {
      const index = s.draft.photoIndex;
      this.service.file(s.id, index).subscribe({
        next: blob => {
          if (this.current()?.id !== s.id) return;
          this.photoBlob = blob;
          this.photoUrl.set(URL.createObjectURL(blob));
        },
        error: err => {
          this.setPhoto.set(false);
          this.photoError.set(serverMessage(err, 'Nie udało się pobrać zdjęcia z Tally.'));
        }
      });
    }
  }

  close(): void {
    this.releasePhoto();
    this.current.set(null);
  }

  chooseTarget(target: string): void {
    this.target.set(target);
    const candidate = this.current()?.candidates.find(c => c.personId === target);
    // Zdjęcie z formularza: dla nowej osoby tak, dla istniejącej tylko gdy jeszcze go nie ma.
    this.setPhoto.set(this.hasPhoto() && !this.photoError() && (target === NEW_PERSON || (candidate ? !candidate.hasPhoto : false)));
  }

  /** Wartość w bazie, gdy różni się od tej z formularza (do porównania przy aktualizacji). */
  existing(key: TextField): string | null {
    const c = this.candidate();
    if (!c) return null;
    const current = c[key] ?? '';
    return current !== (this.value[key] ?? '').trim() ? current || '(puste)' : null;
  }

  existingNameDay(): string | null {
    const c = this.candidate();
    if (!c) return null;
    const same = c.nameDayDay === this.value.nameDayDay && c.nameDayMonth === this.value.nameDayMonth;
    return same ? null : c.nameDayMonth ? this.nameDay(c.nameDayDay, c.nameDayMonth) : '(puste)';
  }

  setMonth(month: string): void {
    this.value.nameDayMonth = month ? +month : null;
  }

  setDay(day: string | number | null): void {
    this.value.nameDayDay = day === null || day === '' ? null : +day;
  }

  save(): void {
    const s = this.current();
    if (!s || this.saving()) return;
    const target = this.target();
    const personId = target === NEW_PERSON ? null : target === OTHER_PERSON ? this.otherPersonId() : target;
    if (target === OTHER_PERSON && !personId) {
      this.toast.error('Wybierz osobę, którą zaktualizować.');
      return;
    }
    if (personId === null && (!this.value.firstName.trim() || !this.value.lastName.trim())) {
      this.toast.error('Nowa osoba musi mieć imię i nazwisko.');
      return;
    }
    this.saving.set(true);
    this.service.apply(s.id, { personId, ...this.value }).subscribe({
      next: result => void this.finishApply(s, result),
      error: err => {
        this.saving.set(false);
        this.toast.error(serverMessage(err, 'Nie udało się zapisać zgłoszenia.'));
      }
    });
  }

  private async finishApply(s: FormSubmission, result: FormSubmission): Promise<void> {
    let message = `Zapisano: ${result.personFullName ?? this.displayName(s)}`;
    if (this.setPhoto() && this.photoBlob && result.personId) {
      try {
        const photo = await this.shrinker.shrink(this.photoBlob);
        await firstValueFrom(this.people.uploadPhoto(result.personId, new File([photo], 'zdjecie.jpg', { type: photo.type || 'image/jpeg' })));
        message += ', ze zdjęciem';
      } catch (err) {
        this.toast.error(serverMessage(err, 'Dane zapisane, ale zdjęcia nie udało się ustawić – dodaj je na karcie osoby.'));
      }
    }
    this.saving.set(false);
    this.toast.success(message + '.');
    this.removeItem(s.id);
    this.pendingCount.update(c => (c === null ? c : Math.max(0, c - 1)));
    this.close();
  }

  reject(s: FormSubmission): void {
    this.service.reject(s.id).subscribe({
      next: () => {
        this.toast.success(`Odrzucono zgłoszenie: ${this.displayName(s)}.`);
        this.removeItem(s.id);
        this.pendingCount.update(c => (c === null ? c : Math.max(0, c - 1)));
        if (this.current()?.id === s.id) this.close();
      },
      error: err => this.toast.error(serverMessage(err, 'Nie udało się odrzucić zgłoszenia.'))
    });
  }

  reopen(s: FormSubmission): void {
    this.service.reopen(s.id).subscribe({
      next: () => {
        this.toast.success(`Zgłoszenie wróciło do przejrzenia: ${this.displayName(s)}.`);
        this.removeItem(s.id);
        this.pendingCount.update(c => (c === null ? c : c + 1));
      },
      error: err => this.toast.error(serverMessage(err, 'Nie udało się przywrócić zgłoszenia.'))
    });
  }

  download(s: FormSubmission, index: number, file: FormSubmissionFile): void {
    this.service.file(s.id, index).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = file.name;
        link.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
      },
      error: err => this.toast.error(serverMessage(err, 'Nie udało się pobrać pliku z Tally.'))
    });
  }

  /** Indeks pliku na wspólnej liście plików zgłoszenia (do pobierania). */
  fileIndex(s: FormSubmission, file: FormSubmissionFile): number {
    return s.files.findIndex(f => f.url === file.url);
  }

  private removeItem(id: string): void {
    this.items.update(list => (list ?? []).filter(i => i.id !== id));
  }

  private releasePhoto(): void {
    const url = this.photoUrl();
    if (url) URL.revokeObjectURL(url);
    this.photoUrl.set(null);
    this.photoError.set(null);
    this.photoBlob = null;
  }

  private fromDraft(d: PersonDraft): EditValue {
    return {
      firstName: d.firstName ?? '',
      lastName: d.lastName ?? '',
      email: d.email ?? '',
      phone: d.phone ?? '',
      addressStreet: d.addressStreet ?? '',
      addressPostalCode: d.addressPostalCode ?? '',
      addressCity: d.addressCity ?? '',
      nameDayDay: d.nameDayDay,
      nameDayMonth: d.nameDayMonth
    };
  }

  private emptyValue(): EditValue {
    return { firstName: '', lastName: '', email: '', phone: '', addressStreet: '', addressPostalCode: '', addressCity: '', nameDayDay: null, nameDayMonth: null };
  }
}
