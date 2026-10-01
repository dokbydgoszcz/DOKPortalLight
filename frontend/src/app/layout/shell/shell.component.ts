import { Component, OnDestroy, OnInit, computed } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { IdleTimeoutService } from '../../core/auth/idle-timeout.service';
import { ToastContainerComponent } from '../../core/notifications/toast-container.component';
import { NAV_ITEMS } from '../nav-items';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, ToastContainerComponent],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss'
})
export class ShellComponent implements OnInit, OnDestroy {
  readonly visibleNavItems = computed(() =>
    NAV_ITEMS.filter(item => item.roles.length === 0 || this.auth.hasAnyRole(item.roles))
  );

  constructor(
    readonly auth: AuthService,
    private readonly idleTimeout: IdleTimeoutService
  ) {}

  ngOnInit(): void {
    this.idleTimeout.start();
  }

  ngOnDestroy(): void {
    this.idleTimeout.stop();
  }

  logout(): void {
    this.auth.logout();
  }
}
