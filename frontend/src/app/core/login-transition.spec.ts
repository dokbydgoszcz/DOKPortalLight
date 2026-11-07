import { describe, expect, it, vi } from 'vitest';
import { ActivatedRouteSnapshot, ViewTransitionInfo } from '@angular/router';
import { animateOnlyAfterLogin } from './login-transition';

const route = (...paths: string[]) => {
  let node: Partial<ActivatedRouteSnapshot> | null = null;
  for (const path of [...paths].reverse()) node = { routeConfig: { path }, firstChild: node as ActivatedRouteSnapshot | null };
  return { routeConfig: null, firstChild: node } as unknown as ActivatedRouteSnapshot;
};

function run(from: ActivatedRouteSnapshot, to: ActivatedRouteSnapshot) {
  const skipTransition = vi.fn();
  animateOnlyAfterLogin({ transition: { skipTransition } as unknown as ViewTransition, from, to } as ViewTransitionInfo);
  return skipTransition;
}

describe('animateOnlyAfterLogin', () => {
  it('animates going from the login page into the app', () => {
    expect(run(route('login'), route('', 'dashboard'))).not.toHaveBeenCalled();
  });

  it('skips every other navigation', () => {
    expect(run(route('', 'dashboard'), route('', 'people'))).toHaveBeenCalled();
    expect(run(route('', 'people'), route('login'))).toHaveBeenCalled();
    expect(run(route('login'), route('login'))).toHaveBeenCalled();
    expect(run({ routeConfig: null, firstChild: null } as unknown as ActivatedRouteSnapshot, route('', 'dashboard'))).toHaveBeenCalled();
  });
});
