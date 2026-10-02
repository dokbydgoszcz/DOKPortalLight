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

  it('does not guard routes that are open to every authenticated user', () => {
    for (const item of NAV_ITEMS.filter(i => !i.permission)) {
      const route = children.find(r => r.path === item.path.replace(/^\//, ''))!;
      expect(route.canActivate).toBeUndefined();
    }
  });
});
