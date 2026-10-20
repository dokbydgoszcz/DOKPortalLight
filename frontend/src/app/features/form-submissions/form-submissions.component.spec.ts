import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ToastService } from '../../core/notifications/toast.service';
import { ImageShrinker } from '../../shared/image-shrink';
import { api, clickByText, setInput, setSelect, setup } from '../../testing/test-helpers';
import { FormSubmissionsComponent } from './form-submissions.component';
import { FormSubmission, FormSubmissionCandidate } from './form-submission.model';

const photoFile = { name: 'ja.jpg', url: 'https://storage.tally.so/ja.jpg', mimeType: 'image/jpeg', size: 10 };
const pdfFile = { name: 'skan.pdf', url: 'https://storage.tally.so/skan.pdf', mimeType: 'application/pdf', size: 10 };

function submission(overrides: Partial<FormSubmission> = {}): FormSubmission {
  return {
    id: 's1', source: 'Tally', formName: 'Dane do systemu', submittedAtUtc: '2026-10-20T13:01:42Z', status: 'Pending',
    fields: [
      { label: 'Imię', type: 'INPUT_TEXT', text: 'Marlena ', files: null },
      { label: 'zdjęcie', type: 'FILE_UPLOAD', text: null, files: [photoFile, pdfFile] },
      { label: 'Uwagi', type: 'TEXTAREA', text: null, files: null }
    ],
    files: [photoFile, pdfFile],
    draft: {
      firstName: 'Marlena', lastName: 'Częstochowska', email: 'lena@example.org', phone: '782 265 165', addressStreet: 'Gabrieli Zapolskiej 10/72',
      addressPostalCode: '85-149', addressCity: 'Bydgoszcz', nameDayDay: 23, nameDayMonth: 10, photoIndex: 0
    },
    candidates: [], personId: null, personFullName: null, reviewedByEmail: null, reviewedAtUtc: null,
    ...overrides
  };
}

function candidate(overrides: Partial<FormSubmissionCandidate> = {}): FormSubmissionCandidate {
  return {
    personId: 'p1', fullName: 'Marlena Częstochowska', matchedBy: 'e-mail', firstName: 'Marlena', lastName: 'Częstochowska', email: 'lena@example.org',
    phone: null, addressStreet: 'Stara 1', addressPostalCode: '85-149', addressCity: 'Bydgoszcz', nameDayDay: null, nameDayMonth: null,
    hasPhoto: true, functions: ['Catechist', 'Inna'], ...overrides
  };
}

const shrink = vi.fn(async (blob: Blob) => blob);

function boot(list: FormSubmission[] = [submission()], count = list.length) {
  const ctx = setup(FormSubmissionsComponent, { providers: [{ provide: ImageShrinker, useValue: { shrink } }] });
  ctx.fixture.detectChanges();
  ctx.http.expectOne(api('/api/form-submissions?status=Pending')).flush(list);
  ctx.http.expectOne(api('/api/form-submissions/pending-count')).flush(count);
  ctx.fixture.detectChanges();
  const toast = TestBed.inject(ToastService);
  return { ...ctx, component: ctx.fixture.componentInstance, success: vi.spyOn(toast, 'success'), error: vi.spyOn(toast, 'error') };
}

const text = (el: HTMLElement) => el.textContent!.replace(/\s+/g, ' ');

describe('FormSubmissionsComponent', () => {
  const original = { create: URL.createObjectURL, revoke: URL.revokeObjectURL };
  beforeEach(() => {
    shrink.mockClear();
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, writable: true, value: vi.fn(() => 'blob:zdjecie') });
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, writable: true, value: vi.fn() });
  });
  afterEach(() => {
    vi.useRealTimers();
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, writable: true, value: original.create });
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, writable: true, value: original.revoke });
  });

  it('lists what waits for review with a suggestion, and switches to saved and rejected submissions', () => {
    const named = submission({ id: 's2', candidates: [candidate({ matchedBy: 'telefon', fullName: 'Piotr Holc' })] });
    const anonymous = submission({ id: 's3', draft: { ...submission().draft, firstName: null, lastName: null, email: null } });
    const ctx = boot([submission({ candidates: [candidate()] }), named, anonymous], 3);
    expect(text(ctx.el)).toContain('Do przejrzenia (3)');
    expect(text(ctx.el)).toContain('aktualizacja: Marlena Częstochowska (e-mail)');
    expect(text(ctx.el)).toContain('nowa osoba? podobne: Piotr Holc');
    expect(text(ctx.el)).toContain('Bez imienia i nazwiska');
    expect(text(ctx.el)).toContain('20.10.2026');

    clickByText(ctx.el, 'Zapisane');
    ctx.http.expectOne(api('/api/form-submissions?status=Applied')).flush([
      submission({ status: 'Applied', personId: 'p9', personFullName: 'Ewa Nowa', reviewedByEmail: 'ja@dok.pl', reviewedAtUtc: '2026-10-20T14:00:00Z' }),
      submission({ id: 'gone', status: 'Applied', personId: null, reviewedByEmail: 'ja@dok.pl', reviewedAtUtc: '2026-10-20T14:00:00Z' })
    ]);
    ctx.fixture.detectChanges();
    expect(text(ctx.el)).toContain('Zapisano do');
    expect((ctx.el.querySelector('a[href="/people/p9"]') as HTMLElement).textContent).toContain('Ewa Nowa');
    clickByText(ctx.el, 'Zapisane');
    ctx.http.expectNone(api('/api/form-submissions?status=Applied'));

    clickByText(ctx.el, 'Odrzucone');
    ctx.http.expectOne(api('/api/form-submissions?status=Rejected')).flush([
      submission({ status: 'Rejected', reviewedByEmail: 'ja@dok.pl', reviewedAtUtc: '2026-10-20T14:00:00Z' })
    ]);
    ctx.fixture.detectChanges();
    expect(text(ctx.el)).toContain('Odrzucił(a)');
    clickByText(ctx.el, 'Przywróć');
    ctx.http.expectOne(api('/api/form-submissions/s1/reopen')).flush(submission());
    ctx.fixture.detectChanges();
    expect(ctx.success).toHaveBeenCalledWith('Zgłoszenie wróciło do przejrzenia: Marlena Częstochowska.');
    expect(text(ctx.el)).toContain('Do przejrzenia (4)');
    expect(text(ctx.el)).toContain('Brak odrzuconych zgłoszeń.');

    clickByText(ctx.el, 'Zapisane');
    ctx.http.expectOne(api('/api/form-submissions?status=Applied')).flush([]);
    ctx.fixture.detectChanges();
    expect(text(ctx.el)).toContain('Nic jeszcze nie zapisano');
  });

  it('shows an empty queue, a loading error, and copes with a failed count or reopen', () => {
    const ctx = setup(FormSubmissionsComponent);
    ctx.fixture.detectChanges();
    expect(text(ctx.el)).toContain('Wczytuję');
    ctx.http.expectOne(api('/api/form-submissions?status=Pending')).flush([]);
    ctx.http.expectOne(api('/api/form-submissions/pending-count')).flush('x', { status: 500, statusText: 'err' });
    ctx.fixture.detectChanges();
    expect(text(ctx.el)).toContain('Nie ma nowych zgłoszeń');
    expect(text(ctx.el)).not.toContain('Do przejrzenia (');

    clickByText(ctx.el, 'Odrzucone');
    ctx.http.expectOne(api('/api/form-submissions?status=Rejected')).flush({ message: 'Brak dostępu.' }, { status: 403, statusText: 'x' });
    ctx.fixture.detectChanges();
    expect(text(ctx.el)).toContain('Brak dostępu.');

    const error = vi.spyOn(TestBed.inject(ToastService), 'error');
    const component = ctx.fixture.componentInstance;
    component.reopen(submission({ status: 'Rejected' }));
    ctx.http.expectOne(api('/api/form-submissions/s1/reopen')).flush({ message: 'Nie da się.' }, { status: 400, statusText: 'x' });
    expect(error).toHaveBeenCalledWith('Nie da się.');
    component.reject(submission());
    ctx.http.expectOne(api('/api/form-submissions/s1/reject')).flush({ message: 'Już przetworzone.' }, { status: 400, statusText: 'x' });
    expect(error).toHaveBeenCalledWith('Już przetworzone.');
    // Przywrócenie i odrzucenie bez znanego licznika nie wymyśla liczby.
    component.reopen(submission({ status: 'Rejected' }));
    ctx.http.expectOne(api('/api/form-submissions/s1/reopen')).flush(submission());
    component.reject(submission());
    ctx.http.expectOne(api('/api/form-submissions/s1/reject')).flush(submission({ status: 'Rejected' }));
    expect(component.pendingCount()).toBeNull();
  });

  it('adds a new person from the cleaned-up answers, with the photo from the form', async () => {
    const ctx = boot();
    clickByText(ctx.el, 'Przejrzyj');
    ctx.http.expectOne(api('/api/form-submissions/s1/files/0')).flush(new Blob(['jpg'], { type: 'image/jpeg' }));
    ctx.fixture.detectChanges();
    await ctx.fixture.whenStable();
    ctx.fixture.detectChanges();

    expect(text(ctx.el)).toContain('Odpowiedzi z formularza');
    expect(text(ctx.el)).toContain('Uwagi —');
    expect((ctx.el.querySelector('#fs-addressStreet') as HTMLInputElement).value).toBe('Gabrieli Zapolskiej 10/72');
    expect((ctx.el.querySelector('select[name="nameDayMonth"]') as HTMLSelectElement).value).toBe('10');
    expect((ctx.el.querySelector('.photo img') as HTMLImageElement).src).toContain('blob:zdjecie');
    expect((ctx.el.querySelector('input[name="setPhoto"]') as HTMLInputElement).checked).toBe(true);
    expect(text(ctx.el)).not.toContain('w bazie');

    setInput(ctx.el, '#fs-addressStreet', 'Zapolskiej 10/72');
    setInput(ctx.el, '#fs-nameday', '24');
    setSelect(ctx.el, 'select[name="nameDayMonth"]', '5');
    ctx.fixture.detectChanges();
    clickByText(ctx.el, 'Dodaj nową osobę');
    const apply = ctx.http.expectOne(api('/api/form-submissions/s1/apply'));
    expect(apply.request.body).toEqual({
      personId: null, firstName: 'Marlena', lastName: 'Częstochowska', email: 'lena@example.org', phone: '782 265 165',
      addressStreet: 'Zapolskiej 10/72', addressPostalCode: '85-149', addressCity: 'Bydgoszcz', nameDayDay: 24, nameDayMonth: 5
    });
    clickByText(ctx.el, 'Dodaj nową osobę');
    ctx.http.expectNone(api('/api/form-submissions/s1/apply'));
    apply.flush(submission({ status: 'Applied', personId: 'p5', personFullName: 'Marlena Częstochowska' }));
    await vi.waitFor(() => expect(shrink).toHaveBeenCalled());
    const upload = await vi.waitFor(() => ctx.http.expectOne(api('/api/people/p5/photo')));
    expect(upload.request.method).toBe('PUT');
    upload.flush({});
    await vi.waitFor(() => expect(ctx.success).toHaveBeenCalledWith('Zapisano: Marlena Częstochowska, ze zdjęciem.'));
    ctx.fixture.detectChanges();
    expect(ctx.el.querySelector('.review-modal')).toBeNull();
    expect(text(ctx.el)).toContain('Do przejrzenia (0)');
    expect(text(ctx.el)).toContain('Nie ma nowych zgłoszeń');
  });

  it('updates the matching person, shows what differs and keeps their existing photo by default', async () => {
    const ctx = boot([submission({ candidates: [candidate()] })]);
    clickByText(ctx.el, 'Przejrzyj');
    ctx.http.expectOne(api('/api/form-submissions/s1/files/0')).flush(new Blob(['jpg'], { type: 'image/jpeg' }));
    ctx.fixture.detectChanges();
    await ctx.fixture.whenStable();
    ctx.fixture.detectChanges();

    expect(text(ctx.el)).toContain('(zgodne: e-mail; Katechista, Inna)');
    expect(text(ctx.el)).toContain('w bazie: Stara 1');
    expect(text(ctx.el)).toContain('w bazie: (puste)');
    expect(text(ctx.el)).toContain('Puste pola nie kasują danych osoby');
    expect(text(ctx.el)).toContain('osoba ma już zdjęcie');
    const photo = ctx.el.querySelector('input[name="setPhoto"]') as HTMLInputElement;
    expect(photo.checked).toBe(false);

    clickByText(ctx.el, 'Zaktualizuj osobę');
    const apply = ctx.http.expectOne(api('/api/form-submissions/s1/apply'));
    expect(apply.request.body.personId).toBe('p1');
    apply.flush(submission({ status: 'Applied', personId: 'p1', personFullName: null }));
    await vi.waitFor(() => expect(ctx.success).toHaveBeenCalledWith('Zapisano: Marlena Częstochowska.'));
    expect(shrink).not.toHaveBeenCalled();
  });

  it('compares name days with the person in the database', () => {
    const ctx = boot([submission({ candidates: [candidate({ nameDayDay: 23, nameDayMonth: 10, addressStreet: 'Gabrieli Zapolskiej 10/72' }), candidate({ personId: 'p2', nameDayDay: 1, nameDayMonth: 1 })] })]);
    const component = ctx.component;
    component.open(component.items()![0]);
    ctx.http.expectOne(api('/api/form-submissions/s1/files/0')).flush(new Blob(['x'], { type: 'image/jpeg' }));
    expect(component.existingNameDay()).toBeNull();
    component.chooseTarget('p2');
    expect(component.existingNameDay()).toBe('1 stycznia');
    component.chooseTarget(component.NEW);
    expect(component.existingNameDay()).toBeNull();
    expect(component.existing('email')).toBeNull();
    component.setDay(null);
    component.setDay('');
    expect(component.value.nameDayDay).toBeNull();
    component.setMonth('');
    expect(component.value.nameDayMonth).toBeNull();
    expect(component.nameDay(null, null)).toBe('—');
  });

  it('updates another person picked by name, and refuses to save without a choice or a name', async () => {
    const ctx = boot([submission({ draft: { ...submission().draft, photoIndex: null, firstName: null } })]);
    clickByText(ctx.el, 'Przejrzyj');
    ctx.fixture.detectChanges();
    expect(ctx.el.querySelector('.photo')).toBeNull();

    clickByText(ctx.el, 'Dodaj nową osobę');
    expect(ctx.error).toHaveBeenCalledWith('Nowa osoba musi mieć imię i nazwisko.');

    clickByText(ctx.el, 'Aktualizacja innej osoby…', 'label');
    ctx.fixture.detectChanges();
    expect(ctx.el.querySelector('app-person-picker')).not.toBeNull();
    clickByText(ctx.el, 'Zaktualizuj osobę');
    expect(ctx.error).toHaveBeenCalledWith('Wybierz osobę, którą zaktualizować.');

    ctx.component.otherPersonId.set('p7');
    clickByText(ctx.el, 'Zaktualizuj osobę');
    const apply = ctx.http.expectOne(api('/api/form-submissions/s1/apply'));
    expect(apply.request.body.personId).toBe('p7');
    apply.flush({ message: 'Ten e-mail ma już w bazie Jan Nowak.' }, { status: 400, statusText: 'x' });
    expect(ctx.error).toHaveBeenCalledWith('Ten e-mail ma już w bazie Jan Nowak.');
    ctx.fixture.detectChanges();
    expect((ctx.el.querySelector('.modal-foot .btn.primary') as HTMLButtonElement).disabled).toBe(false);

    clickByText(ctx.el, 'Anuluj');
    ctx.fixture.detectChanges();
    expect(ctx.el.querySelector('.review-modal')).toBeNull();
  });

  it('handles a photo Tally no longer serves, and a photo that could not be set', async () => {
    const ctx = boot([submission(), submission({ id: 's2' })]);
    ctx.component.open(ctx.component.items()![0]);
    ctx.http.expectOne(api('/api/form-submissions/s1/files/0'))
      .flush(new Blob([JSON.stringify({ message: 'Tally nie oddał pliku.' })], { type: 'application/json' }), { status: 400, statusText: 'x' });
    await vi.waitFor(() => expect(ctx.component.photoError()).toBe('Tally nie oddał pliku.'));
    ctx.fixture.detectChanges();
    expect((ctx.el.querySelector('input[name="setPhoto"]') as HTMLInputElement).disabled).toBe(true);
    expect(ctx.component.setPhoto()).toBe(false);

    // Zdjęcie dojeżdża po zamknięciu okna – nie trafia do innego zgłoszenia.
    ctx.component.open(ctx.component.items()![1]);
    const late = ctx.http.expectOne(api('/api/form-submissions/s2/files/0'));
    ctx.component.close();
    late.flush(new Blob(['x'], { type: 'image/jpeg' }));
    expect(ctx.component.photoUrl()).toBeNull();

    ctx.component.open(ctx.component.items()![1]);
    ctx.http.expectOne(api('/api/form-submissions/s2/files/0')).flush(new Blob(['x'], { type: 'image/jpeg' }));
    ctx.fixture.detectChanges();
    const input = ctx.el.querySelector('input[name="setPhoto"]') as HTMLInputElement;
    input.checked = true;
    input.dispatchEvent(new Event('change'));
    shrink.mockRejectedValueOnce(new Error('canvas'));
    ctx.component.save();
    ctx.http.expectOne(api('/api/form-submissions/s2/apply')).flush(submission({ id: 's2', status: 'Applied', personId: 'p5', personFullName: 'M C' }));
    await vi.waitFor(() => expect(ctx.error).toHaveBeenCalledWith('Dane zapisane, ale zdjęcia nie udało się ustawić – dodaj je na karcie osoby.'));
    expect(ctx.success).toHaveBeenCalledWith('Zapisano: M C.');
  });

  it('rejects from the review window and downloads attached files', async () => {
    vi.useFakeTimers();
    const ctx = boot();
    ctx.component.open(ctx.component.items()![0]);
    ctx.http.expectOne(api('/api/form-submissions/s1/files/0')).flush(new Blob(['x'], { type: 'image/jpeg' }));
    ctx.fixture.detectChanges();

    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    clickByText(ctx.el, 'skan.pdf');
    ctx.http.expectOne(api('/api/form-submissions/s1/files/1')).flush(new Blob(['pdf'], { type: 'application/pdf' }));
    expect(click).toHaveBeenCalled();
    vi.advanceTimersByTime(1000);
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:zdjecie');
    clickByText(ctx.el, 'skan.pdf');
    ctx.http.expectOne(api('/api/form-submissions/s1/files/1')).flush(new Blob([JSON.stringify({ message: 'Link wygasł.' })], { type: 'application/json' }), { status: 400, statusText: 'x' });
    await vi.waitFor(() => expect(ctx.error).toHaveBeenCalledWith('Link wygasł.'));
    click.mockRestore();

    clickByText(ctx.el, 'Odrzuć', '.modal-foot button');
    ctx.http.expectOne(api('/api/form-submissions/s1/reject')).flush(submission({ status: 'Rejected' }));
    ctx.fixture.detectChanges();
    expect(ctx.success).toHaveBeenCalledWith('Odrzucono zgłoszenie: Marlena Częstochowska.');
    expect(ctx.el.querySelector('.review-modal')).toBeNull();

    ctx.fixture.destroy();
  });

  it('rejecting from the list leaves another open review alone', () => {
    const ctx = boot([submission(), submission({ id: 's2', draft: { ...submission().draft, photoIndex: null } })]);
    ctx.component.open(ctx.component.items()![1]);
    ctx.component.reject(ctx.component.items()![0]);
    ctx.http.expectOne(api('/api/form-submissions/s1/reject')).flush(submission({ status: 'Rejected' }));
    expect(ctx.component.current()?.id).toBe('s2');
    expect(ctx.component.pendingCount()).toBe(1);
  });

  it('copes with a submission that answered almost nothing', async () => {
    const empty = { firstName: null, lastName: null, email: null, phone: null, addressStreet: null, addressPostalCode: null, addressCity: null, nameDayDay: null, nameDayMonth: null };
    const bare = submission({
      formName: null, fields: [], files: [photoFile], draft: { ...empty, photoIndex: 0 },
      candidates: [candidate({ ...empty, fullName: 'Jan Bez', matchedBy: 'imię i nazwisko', functions: [], hasPhoto: false })]
    });
    const ctx = boot([bare]);
    expect(text(ctx.el)).toContain('Tally');
    clickByText(ctx.el, 'Przejrzyj');
    ctx.http.expectOne(api('/api/form-submissions/s1/files/0')).flush(new Blob(['x'], { type: 'image/jpeg' }));
    ctx.fixture.detectChanges();
    await ctx.fixture.whenStable();
    ctx.fixture.detectChanges();
    expect(text(ctx.el)).toContain('(zgodne: imię i nazwisko)');
    expect(ctx.component.existing('firstName')).toBeNull();
    expect(ctx.component.setPhoto()).toBe(true);

    ctx.component.pendingCount.set(null);
    shrink.mockResolvedValueOnce(new Blob(['x']));
    clickByText(ctx.el, 'Zaktualizuj osobę');
    ctx.http.expectOne(api('/api/form-submissions/s1/apply')).flush(submission({ status: 'Applied', personId: 'p1', personFullName: 'Jan Bez' }));
    const upload = await vi.waitFor(() => ctx.http.expectOne(api('/api/people/p1/photo')));
    expect((upload.request.body as FormData).get('file')).toHaveProperty('type', 'image/jpeg');
    upload.flush({});
    await vi.waitFor(() => expect(ctx.success).toHaveBeenCalledWith('Zapisano: Jan Bez, ze zdjęciem.'));
    expect(ctx.component.pendingCount()).toBeNull();
  });
});
