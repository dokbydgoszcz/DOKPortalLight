import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import { MailingComponent } from './mailing.component';
import { MailingCampaign } from './mailing-campaign.model';
import { ToastService } from '../../core/notifications/toast.service';
import { api, clickByText, paged, setInput, setSelect, setup, textOf } from '../../testing/test-helpers';

const draft: MailingCampaign = {
  id: 'c1', subject: 'Zaproszenie', body: 'Treść', group: 'DokCases', recipientCount: 12,
  status: 'Draft', createdAtUtc: '2026-09-22T00:00:00Z', sentAtUtc: null
};
const sent: MailingCampaign = { ...draft, id: 'c2', subject: 'Podsumowanie', group: 'CandidatesSksp', status: 'Sent', sentAtUtc: '2026-09-23T00:00:00Z' };
const url = api('/api/mailing/campaigns');

function boot(items: MailingCampaign[] = [draft, sent]) {
  const ctx = setup(MailingComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(url).flush(items);
  ctx.fixture.detectChanges();
  return ctx;
}

const toastMessages = () => TestBed.inject(ToastService).toasts().map(t => t.message);

describe('MailingComponent', () => {
  it('renders campaigns with their group, status and recipients', () => {
    const { el } = boot();

    const text = textOf(el);
    expect(text).toContain('Zaproszenie');
    expect(text).toContain('Podopieczni DOK');
    expect(text).toContain('Kandydaci SKŚP');
    expect(text).toContain('Szkic');
    expect(text).toContain('Wysłano');
  });

  it('offers sending only for drafts', () => {
    const { el } = boot();

    const sendButtons = Array.from(el.querySelectorAll('tbody button')).filter(b => b.textContent!.includes('Wyślij'));
    expect(sendButtons.length).toBe(1);
  });

  it('shows a toast when the campaigns cannot be loaded', () => {
    const { fixture, http } = setup(MailingComponent);
    fixture.detectChanges();

    http.expectOne(url).flush('x', { status: 500, statusText: 'Server Error' });

    expect(toastMessages()).toContain('Nie udało się wczytać listy kampanii.');
  });

  describe('adding a campaign', () => {
    async function openForm() {
      const ctx = boot();
      clickByText(ctx.el, 'Nowa kampania');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('offers every recipient group and posts the typed campaign', async () => {
      const ctx = await openForm();
      expect(ctx.el.querySelectorAll('select[name="group"] option').length).toBe(7);

      setInput(ctx.el, 'input[name="subject"]', 'Rekolekcje');
      setSelect(ctx.el, 'select[name="group"]', 'Missionaries');
      setInput(ctx.el, 'textarea[name="body"]', 'Zapraszamy w sobotę.');
      ctx.fixture.detectChanges();
      clickByText(ctx.el, 'Zapisz', '.modal-foot button');

      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
      expect(req.request.body).toEqual({ subject: 'Rekolekcje', body: 'Zapraszamy w sobotę.', group: 'Missionaries' });
      req.flush(draft);
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Dodano kampanię.');
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([draft]);
    });

    const saveButton = (el: HTMLElement) =>
      Array.from(el.querySelectorAll<HTMLButtonElement>('.modal-foot button')).find(b => b.textContent!.includes('Zapisz'))!;

    it('keeps saving disabled, with a hint, until both the subject and the body have text', async () => {
      const ctx = await openForm();

      expect(saveButton(ctx.el).disabled).toBe(true);
      expect(textOf(ctx.el)).toContain('Uzupełnij pola oznaczone *');
      expect(ctx.el.querySelectorAll('.modal .field .required').length).toBe(2);
      saveButton(ctx.el).click();
      ctx.http.expectNone(r => r.method === 'POST');

      setInput(ctx.el, 'input[name="subject"]', 'Rekolekcje');
      ctx.fixture.detectChanges();
      expect(saveButton(ctx.el).disabled).toBe(true);

      setInput(ctx.el, 'textarea[name="body"]', '   ');
      ctx.fixture.detectChanges();
      expect(saveButton(ctx.el).disabled).toBe(true);

      setInput(ctx.el, 'textarea[name="body"]', 'Zapraszamy.');
      ctx.fixture.detectChanges();
      expect(saveButton(ctx.el).disabled).toBe(false);
      expect(textOf(ctx.el)).not.toContain('Uzupełnij pola oznaczone *');
    });

    it('limits the subject to 200 characters, as the server does', async () => {
      const ctx = await openForm();

      expect((ctx.el.querySelector('input[name="subject"]') as HTMLInputElement).maxLength).toBe(200);
    });

    it('defaults to the candidates group, shows a toast when adding fails and closes on cancel', async () => {
      const ctx = await openForm();
      setInput(ctx.el, 'input[name="subject"]', 'Temat');
      setInput(ctx.el, 'textarea[name="body"]', 'Treść');
      ctx.fixture.detectChanges();

      clickByText(ctx.el, 'Zapisz', '.modal-foot button');
      const req = ctx.http.expectOne(r => r.method === 'POST');
      expect(req.request.body.group).toBe('CandidatesSksp');
      req.flush('x', { status: 400, statusText: 'Bad Request' });
      expect(toastMessages()).toContain('Nie udało się dodać kampanii.');

      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();
    });
  });

  describe('sending a campaign', () => {
    it('sends the draft, confirms and reloads', () => {
      const ctx = boot();

      clickByText(ctx.el, 'Wyślij');
      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === `${url}/c1/send`);
      expect(req.request.body).toEqual({});
      req.flush({ ...draft, status: 'Sent' });

      expect(toastMessages()).toContain('Kampania wysłana.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([sent]);
    });

    it('reports a partly failed send with the number of addresses that did not get the message', () => {
      const ctx = boot();

      clickByText(ctx.el, 'Wyślij');
      ctx.http.expectOne(r => r.method === 'POST').flush({ ...draft, status: 'Sent', recipientCount: 10, failedCount: 2 });

      expect(toastMessages()).toContain('Wysłano do 10 odbiorców. Nie udało się wysłać do 2 adresów – sprawdź ich poprawność.');
      expect(toastMessages()).not.toContain('Kampania wysłana.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([sent]);
    });

    it('disables the send button while a send is in progress, so a double click cannot send twice', () => {
      const ctx = boot();
      const button = Array.from(ctx.el.querySelectorAll<HTMLButtonElement>('button')).find(b => b.textContent!.trim() === 'Wyślij')!;

      button.click();
      ctx.fixture.detectChanges();

      expect(button.disabled).toBe(true);
      button.click();
      ctx.http.expectOne(r => r.method === 'POST').flush({ ...draft, status: 'Sent' });
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([sent]);
      ctx.fixture.detectChanges();
    });

    it('lets the user try again after a refused send', () => {
      const ctx = boot();
      const button = Array.from(ctx.el.querySelectorAll<HTMLButtonElement>('button')).find(b => b.textContent!.trim() === 'Wyślij')!;

      button.click();
      ctx.http.expectOne(r => r.method === 'POST').flush({ title: 'x' }, { status: 400, statusText: 'Bad Request' });
      ctx.fixture.detectChanges();

      expect(button.disabled).toBe(false);
    });

    it('shows the number of failed addresses next to the recipients of a sent campaign', () => {
      const { el } = boot([{ ...sent, recipientCount: 10, failedCount: 2 }]);

      expect(textOf(el)).toContain('10');
      expect(textOf(el)).toContain('błędy: 2');
    });

    it('shows the message from the server when sending is refused', () => {
      const ctx = boot();

      clickByText(ctx.el, 'Wyślij');
      ctx.http.expectOne(r => r.method === 'POST')
        .flush({ title: 'Wysyłanie e-maili nie jest skonfigurowane (brak Smtp:Host).' }, { status: 400, statusText: 'Bad Request' });

      expect(toastMessages()).toContain('Wysyłanie e-maili nie jest skonfigurowane (brak Smtp:Host).');
    });

    it('falls back to a generic message when the server gives no reason', () => {
      const ctx = boot();

      clickByText(ctx.el, 'Wyślij');
      ctx.http.expectOne(r => r.method === 'POST').flush('x', { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Nie udało się wysłać kampanii.');
    });
  });

  describe('deleting a draft', () => {
    const rowOf = (el: HTMLElement, text: string) =>
      Array.from(el.querySelectorAll('tbody tr')).find(r => r.textContent!.includes(text)) as HTMLElement;
    const deleteLink = (el: HTMLElement, text: string) =>
      Array.from(rowOf(el, text).querySelectorAll<HTMLElement>('.link')).find(l => l.textContent!.trim() === 'Usuń');

    it('offers deleting only for drafts', () => {
      const { el } = boot();

      expect(deleteLink(el, 'Zaproszenie')).toBeDefined();
      expect(deleteLink(el, 'Podsumowanie')).toBeUndefined();
    });

    it('deletes the draft after confirmation and reloads the list', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      deleteLink(ctx.el, 'Zaproszenie')!.click();

      ctx.http.expectOne(r => r.method === 'DELETE' && r.url === `${url}/c1`).flush(null, { status: 204, statusText: 'No Content' });
      expect(toastMessages()).toContain('Szkic usunięty.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([sent]);
      ctx.fixture.detectChanges();
      expect(textOf(ctx.el)).not.toContain('Zaproszenie');
    });

    it('does nothing when the confirmation is declined', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(false);
      const ctx = boot();

      deleteLink(ctx.el, 'Zaproszenie')!.click();

      ctx.http.expectNone(r => r.method === 'DELETE');
    });

    it('shows the server message, or a generic one, when deleting fails', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      deleteLink(ctx.el, 'Zaproszenie')!.click();
      ctx.http.expectOne(r => r.method === 'DELETE').flush({ title: 'Wysłanej kampanii nie można usunąć – zostaje w historii.' }, { status: 400, statusText: 'Bad Request' });
      deleteLink(ctx.el, 'Zaproszenie')!.click();
      ctx.http.expectOne(r => r.method === 'DELETE').flush('x', { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Wysłanej kampanii nie można usunąć – zostaje w historii.');
      expect(toastMessages()).toContain('Nie udało się usunąć szkicu.');
    });

    it('is hidden from users who cannot manage mailing', () => {
      const ctx = setup(MailingComponent, { granted: ['Mailing.View'] });
      ctx.fixture.detectChanges();
      ctx.http.expectOne(url).flush([draft]);
      ctx.fixture.detectChanges();

      expect(textOf(ctx.el)).not.toContain('Usuń');
    });
  });

  describe('test message', () => {
    const testUrl = api('/api/mailing/test-email');

    it('sends a test message to the logged-in user and says where it went', () => {
      const ctx = boot();

      clickByText(ctx.el, 'Wyślij wiadomość testową', 'button');
      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === testUrl);
      req.flush({ sentTo: 'dyrektor@example.org' });

      expect(toastMessages()).toContain('Wysłano wiadomość testową na adres dyrektor@example.org.');
    });

    it('is disabled while sending, so a double click does not send twice', () => {
      const ctx = boot();
      const button = () => Array.from(ctx.el.querySelectorAll('button')).find(b => b.textContent!.includes('Wyślij wiadomość testową'))!;

      button().click();
      ctx.fixture.detectChanges();
      expect(button().disabled).toBe(true);
      button().click();

      const req = ctx.http.expectOne(r => r.url === testUrl);
      req.flush({ sentTo: 'a@example.org' });
      ctx.fixture.detectChanges();
      expect(button().disabled).toBe(false);
    });

    it('shows the reason the server gives, or a generic one', () => {
      const ctx = boot();

      clickByText(ctx.el, 'Wyślij wiadomość testową', 'button');
      ctx.http.expectOne(r => r.url === testUrl).flush(
        { title: 'Nie udało się wysłać wiadomości: 5.7.57 Client not authenticated' }, { status: 400, statusText: 'Bad Request' });
      ctx.fixture.detectChanges();
      clickByText(ctx.el, 'Wyślij wiadomość testową', 'button');
      ctx.http.expectOne(r => r.url === testUrl).flush('x', { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Nie udało się wysłać wiadomości: 5.7.57 Client not authenticated');
      expect(toastMessages()).toContain('Nie udało się wysłać wiadomości testowej.');
    });

    it('is hidden from users who cannot manage mailing', () => {
      const ctx = setup(MailingComponent, { granted: ['Mailing.View'] });
      ctx.fixture.detectChanges();
      ctx.http.expectOne(url).flush([draft]);
      ctx.fixture.detectChanges();

      expect(textOf(ctx.el)).not.toContain('Wyślij wiadomość testową');
    });
  });
  describe('drafts, recipients and attachments', () => {
    it('edits a draft in place', async () => {
      const ctx = boot();
      clickByText(ctx.el, 'Edytuj');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      setInput(ctx.el, 'input[name="subject"]', 'Poprawiony temat');
      ctx.fixture.detectChanges();
      clickByText(ctx.el, 'Zapisz', '.modal-foot button');

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/c1`);
      expect(req.request.body.subject).toBe('Poprawiony temat');
      expect(req.request.body.personIds).toBeUndefined();
      req.flush({ ...draft, subject: 'Poprawiony temat' });
      expect(toastMessages()).toContain('Zapisano zmiany w szkicu.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([draft]);
    });

    it('builds a custom list from a people search and sends the picked ids', async () => {
      const ctx = boot();
      clickByText(ctx.el, 'Nowa kampania');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      setSelect(ctx.el, 'select[name="group"]', 'Custom');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      setInput(ctx.el, 'input[name="personSearch"]', 'Ko');
      ctx.http.expectOne(r => r.url === api('/api/people') && r.params.get('query') === 'Ko').flush(paged([
        { id: 'p1', firstName: 'Jan', lastName: 'Kowalski', fullName: 'Jan Kowalski', email: 'jan@example.org' },
        { id: 'p2', firstName: 'Ewa', lastName: 'Kot', fullName: 'Ewa Kot', email: null }
      ]));
      ctx.fixture.detectChanges();
      clickByText(ctx.el, 'Dodaj');
      ctx.fixture.detectChanges();
      setInput(ctx.el, 'input[name="subject"]', 'Spotkanie');
      setInput(ctx.el, 'textarea[name="body"]', 'Zapraszamy.');
      ctx.fixture.detectChanges();

      clickByText(ctx.el, 'Zapisz', '.modal-foot button');

      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
      expect(req.request.body).toEqual({ subject: 'Spotkanie', body: 'Zapraszamy.', group: 'Custom', personIds: ['p1'] });
    });

    it('previews the recipients of a group, flagging those without e-mail', async () => {
      const ctx = boot();
      clickByText(ctx.el, 'Nowa kampania');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      setSelect(ctx.el, 'select[name="group"]', 'Pastors');
      ctx.fixture.detectChanges();

      clickByText(ctx.el, 'Pokaż odbiorców');
      ctx.http.expectOne(r => r.url === api('/api/mailing/recipients') && r.params.get('group') === 'Pastors').flush([
        { personId: 'p1', fullName: 'ks. Jan Nowak', email: 'jan@example.org' },
        { personId: 'p2', fullName: 'ks. Adam Bez', email: null }
      ]);
      ctx.fixture.detectChanges();

      const text = textOf(ctx.el);
      expect(text).toContain('Osób: 2, z adresem e-mail: 1');
      expect(text).toContain('brak e-maila');
    });

    it('opens the attachments of a campaign', () => {
      const ctx = boot([{ ...draft, attachments: [{ id: 'a1', fileName: 'plan.pdf', contentType: 'application/pdf', sizeBytes: 10, uploadedAtUtc: '2026-10-01T10:00:00Z' }] }]);
      clickByText(ctx.el, 'Załączniki');
      ctx.fixture.detectChanges();

      expect(textOf(ctx.el)).toContain('plan.pdf');
      expect(textOf(ctx.el)).toContain('łącznie do 15 MB');
    });
  });
});
