import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { UnsubscribeComponent } from './unsubscribe.component';
import { api, setup, textOf } from '../../testing/test-helpers';

const url = api('/api/mailing/subscription/abc');

async function boot() {
  const ctx = setup(UnsubscribeComponent, {
    providers: [{ provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ token: 'abc' }) } } }]
  });
  ctx.fixture.detectChanges();
  return ctx;
}

const settle = async (ctx: Awaited<ReturnType<typeof boot>>) => {
  for (let i = 0; i < 3; i++) {
    await new Promise(r => setTimeout(r, 0));
    ctx.fixture.detectChanges();
  }
};

describe('UnsubscribeComponent', () => {
  it('asks before unsubscribing, then lets the person change their mind', async () => {
    const ctx = await boot();
    expect(textOf(ctx.el)).toContain('Wczytywanie');
    ctx.http.expectOne(url).flush({ firstName: 'Jan', optedOut: false });
    await settle(ctx);
    expect(textOf(ctx.el)).toContain('Jan, czy na pewno');

    (ctx.el.querySelector('button') as HTMLButtonElement).click();
    const post = ctx.http.expectOne(`${url}/unsubscribe`);
    expect(post.request.method).toBe('POST');
    post.flush({ firstName: 'Jan', optedOut: true });
    await settle(ctx);
    expect(textOf(ctx.el)).toContain('nie będziesz już dostawać');

    (ctx.el.querySelector('button') as HTMLButtonElement).click();
    ctx.http.expectOne(`${url}/resubscribe`).flush({ firstName: 'Jan', optedOut: false });
    await settle(ctx);
    expect(textOf(ctx.el)).toContain('Wypisz mnie');
  });

  it('explains a broken link and a failed save', async () => {
    const bad = await boot();
    bad.http.expectOne(url).flush(null, { status: 404, statusText: 'Not Found' });
    await settle(bad);
    expect(textOf(bad.el)).toContain('link jest nieprawidłowy');
  });

  it('reports when the change could not be saved', async () => {
    const ctx = await boot();
    ctx.http.expectOne(url).flush({ firstName: 'Jan', optedOut: false });
    await settle(ctx);
    (ctx.el.querySelector('button') as HTMLButtonElement).click();
    ctx.http.expectOne(`${url}/unsubscribe`).flush(null, { status: 500, statusText: 'Error' });
    await settle(ctx);
    expect(textOf(ctx.el)).toContain('Nie udało się zapisać');
  });
});
