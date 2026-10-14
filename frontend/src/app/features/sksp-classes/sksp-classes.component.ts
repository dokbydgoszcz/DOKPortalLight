import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { ToastService } from '../../core/notifications/toast.service';
import { serverMessage } from '../../shared/http-error';
import { DEFAULT_END, DEFAULT_START, hhmm, timeRange } from '../meetings/meeting.model';
import { romanYear } from '../candidates/candidate.model';
import { SkspClass, SkspClassAttendee, SkspClassFormValue, presenceSummary } from './sksp-class.model';
import { SkspClassesService } from './sksp-classes.service';

/** Zajęcia SKŚP z imienną listą obecności – z nich liczy się frekwencja kandydatów. */
@Component({
  selector: 'app-sksp-classes',
  standalone: true,
  imports: [FormsModule, DatePipe],
  templateUrl: './sksp-classes.component.html',
  styleUrl: './sksp-classes.component.scss'
})
export class SkspClassesComponent implements OnInit {
  readonly classes = signal<SkspClass[]>([]);
  /** 0 = wszystkie lata. */
  readonly yearFilter = signal(0);
  readonly expandedId = signal<string | null>(null);
  readonly isFormOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly canManage = inject(AuthService).hasPermission(Permissions.CandidatesManage);
  readonly romanYear = romanYear;
  readonly timeRange = timeRange;
  readonly presenceSummary = presenceSummary;
  formValue: SkspClassFormValue = { year: 1, classDate: '', topic: '' };

  private readonly service = inject(SkspClassesService);
  private readonly toast = inject(ToastService);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.service.list(this.yearFilter() || undefined).subscribe({
      next: classes => this.classes.set(classes),
      error: () => this.toast.error('Nie udało się wczytać zajęć SKŚP.')
    });
  }

  onYearFilter(value: string): void {
    this.yearFilter.set(Number(value));
    this.load();
  }

  toggle(skspClass: SkspClass): void {
    this.expandedId.set(this.expandedId() === skspClass.id ? null : skspClass.id);
  }

  openAddForm(): void {
    this.editingId.set(null);
    this.formValue = { year: this.yearFilter() || 1, classDate: '', startTime: DEFAULT_START, endTime: DEFAULT_END, topic: '' };
    this.isFormOpen.set(true);
  }

  openEditForm(skspClass: SkspClass): void {
    this.editingId.set(skspClass.id);
    this.formValue = {
      year: skspClass.year,
      classDate: skspClass.classDate,
      startTime: hhmm(skspClass.startTime) || undefined,
      endTime: hhmm(skspClass.endTime) || undefined,
      topic: skspClass.topic,
      notes: skspClass.notes ?? undefined
    };
    this.isFormOpen.set(true);
  }

  get canSave(): boolean {
    const { classDate, topic, startTime, endTime } = this.formValue;
    return !!classDate && !!topic.trim() && !(endTime && !startTime) && !(startTime && endTime && endTime <= startTime);
  }

  save(): void {
    const id = this.editingId();
    const value: SkspClassFormValue = {
      ...this.formValue,
      year: Number(this.formValue.year),
      startTime: this.formValue.startTime || undefined,
      endTime: this.formValue.endTime || undefined
    };
    const request$ = id ? this.service.update(id, value) : this.service.create(value);
    request$.subscribe({
      next: saved => {
        this.isFormOpen.set(false);
        this.toast.success(id ? 'Zapisano zmiany.' : `Dodano zajęcia – na liście obecności: ${saved.attendees.length} os.`);
        if (!id) this.expandedId.set(saved.id);
        this.load();
      },
      error: err => this.toast.error(serverMessage(err, id ? 'Nie udało się zapisać zmian.' : 'Nie udało się dodać zajęć.'))
    });
  }

  private replace(updated: SkspClass): void {
    this.classes.update(list => list.map(c => (c.id === updated.id ? updated : c)));
  }

  /** Klik w zaznaczony stan zdejmuje zaznaczenie. */
  mark(skspClass: SkspClass, attendee: SkspClassAttendee, present: boolean): void {
    const isPresent = attendee.isPresent === present ? null : present;
    this.service.setAttendance(skspClass.id, [{ candidateId: attendee.candidateId, isPresent }]).subscribe({
      next: updated => this.replace(updated),
      error: () => this.toast.error('Nie udało się zapisać obecności.')
    });
  }

  /** Wszyscy jeszcze nieoznaczeni – obecni. */
  markRestPresent(skspClass: SkspClass): void {
    const items = skspClass.attendees.filter(a => a.isPresent === null).map(a => ({ candidateId: a.candidateId, isPresent: true }));
    if (items.length === 0) return;
    this.service.setAttendance(skspClass.id, items).subscribe({
      next: updated => this.replace(updated),
      error: () => this.toast.error('Nie udało się zapisać obecności.')
    });
  }

  refresh(skspClass: SkspClass): void {
    this.service.refreshAttendees(skspClass.id).subscribe({
      next: updated => {
        const added = updated.attendees.length - skspClass.attendees.length;
        this.replace(updated);
        this.toast.success(added > 0 ? `Dopisano do listy: ${added} os.` : 'Lista obecności jest aktualna.');
      },
      error: () => this.toast.error('Nie udało się uzupełnić listy.')
    });
  }

  remove(skspClass: SkspClass): void {
    if (!confirm(`Usunąć zajęcia „${skspClass.topic}” z dnia ${skspClass.classDate}? Obecność z tych zajęć przestanie się liczyć do frekwencji.`)) return;
    this.service.delete(skspClass.id).subscribe({
      next: () => {
        this.toast.success('Zajęcia usunięte.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się usunąć zajęć.')
    });
  }
}
