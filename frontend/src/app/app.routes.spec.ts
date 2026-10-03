import { describe, it, expect } from 'vitest';
import { routes } from './app.routes';
import { NAV_ITEMS } from './layout/nav-items';

describe('routes', () => {
  const children = routes.find(r => r.path === '')!.children!;

  it('protects every menu item that requires a permission with a canActivate guard', () => {
    const restricted = NAV_ITEMS.filter(item => item.permission);

    expect(restricted.length).toBeGreaterThan(0);
    for (const item of restricted) {
      const route = children.find(r => r.path === item.path.replace(/^\//, ''));
      expect(route, `brak trasy dla ${item.path}`).toBeDefined();
      expect(route!.canActivate?.length, `brak guarda na ${item.path}`).toBe(1);
    }
  });

  it('lazy-loads a component class for every feature route and the login page', async () => {
    const lazy = [routes.find(r => r.path === 'login')!, ...children].filter(r => r.loadComponent);

    expect(lazy.length).toBeGreaterThan(15);
    for (const route of lazy) {
      const component = await (route.loadComponent as () => Promise<unknown>)();
      expect(typeof component, `trasa ${route.path}`).toBe('function');
    }
  });

  it('redirects unknown urls to the login page and the root to the dashboard', () => {
    expect(routes.find(r => r.path === '**')?.redirectTo).toBe('login');
    expect(children.find(r => r.path === '')?.redirectTo).toBe('dashboard');
  });

  it('does not guard routes that are open to every authenticated user', () => {
    for (const item of NAV_ITEMS.filter(i => !i.permission)) {
      const route = children.find(r => r.path === item.path.replace(/^\//, ''))!;
      expect(route.canActivate).toBeUndefined();
    }
  });
});
