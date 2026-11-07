import { ActivatedRouteSnapshot, ViewTransitionInfo } from '@angular/router';

/** Ścieżka najgłębszej trasy (np. 'login', 'dashboard'). */
function leafPath(snapshot: ActivatedRouteSnapshot | null | undefined): string | undefined {
  let node = snapshot;
  while (node?.firstChild) node = node.firstChild;
  return node?.routeConfig?.path;
}

/**
 * Animacja tylko po zalogowaniu: okno logowania odjeżdża w prawo, a aplikacja wjeżdża
 * (style ::view-transition-* w styles.scss). Każda inna nawigacja przechodzi bez animacji.
 */
export function animateOnlyAfterLogin({ transition, from, to }: ViewTransitionInfo): void {
  if (leafPath(from) !== 'login' || leafPath(to) === 'login') transition.skipTransition();
}
