import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, it, expect } from 'vitest';
import { HasPermissionDirective } from './has-permission.directive';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  standalone: true,
  imports: [HasPermissionDirective],
  template: `<button *appHasPermission="'People.Manage'">Dodaj</button>`
})
class KnownPermissionHost {}

@Component({
  standalone: true,
  imports: [HasPermissionDirective],
  template: `<button *appHasPermission="'People.Fly'">Dodaj</button>`
})
class UnknownPermissionHost {}

function render<T>(component: new () => T, granted: string[]) {
  TestBed.configureTestingModule({
    imports: [component],
    providers: [{ provide: AuthService, useValue: { hasPermission: (p: string) => granted.includes(p) } }]
  });
  const fixture = TestBed.createComponent(component);
  fixture.detectChanges();
  return fixture;
}

describe('HasPermissionDirective', () => {
  it('renders the element when the user has the permission', () => {
    const fixture = render(KnownPermissionHost, ['People.Manage']);

    expect(fixture.nativeElement.querySelector('button')).not.toBeNull();
  });

  it('does not render the element without the permission', () => {
    const fixture = render(KnownPermissionHost, ['Candidates.Manage']);

    expect(fixture.nativeElement.querySelector('button')).toBeNull();
  });

  it('throws for a permission name that is not in the catalog', () => {
    expect(() => render(UnknownPermissionHost, ['People.Manage'])).toThrow('Nieznane uprawnienie: People.Fly');
  });
});
