import { Component, OnDestroy, OnInit, computed, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { IdleTimeoutService } from '../../core/auth/idle-timeout.service';
import { ToastContainerComponent } from '../../core/notifications/toast-container.component';
import { NAV_ITEMS } from '../nav-items';
import { AppFooterComponent } from '../app-footer.component';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, ToastContainerComponent, AppFooterComponent],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss'
})
export class ShellComponent implements OnInit, OnDestroy {
  readonly visibleNavItems = computed(() =>
    NAV_ITEMS.filter(item => !item.permission || this.auth.hasPermission(item.permission))
  );

  readonly isSidebarOpen = signal(false);

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

  toggleSidebar(): void {
    this.isSidebarOpen.update(open => !open);
  }

  closeSidebar(): void {
    this.isSidebarOpen.set(false);
  }
}
