import { Component, computed } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { NAV_ITEMS } from '../nav-items';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss'
})
export class ShellComponent {
  readonly visibleNavItems = computed(() =>
    NAV_ITEMS.filter(item => item.roles.length === 0 || this.auth.hasAnyRole(item.roles))
  );

  constructor(readonly auth: AuthService) {}

  logout(): void {
    this.auth.logout();
  }
}
