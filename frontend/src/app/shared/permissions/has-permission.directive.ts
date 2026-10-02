import { Directive, Input, TemplateRef, ViewContainerRef, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { ALL_PERMISSIONS } from '../../core/auth/permissions';

@Directive({ selector: '[appHasPermission]', standalone: true })
export class HasPermissionDirective {
  private readonly auth = inject(AuthService);
  private readonly templateRef = inject(TemplateRef<unknown>);
  private readonly viewContainer = inject(ViewContainerRef);

  @Input({ required: true }) set appHasPermission(permission: string) {
    if (!ALL_PERMISSIONS.includes(permission)) {
      throw new Error(`Nieznane uprawnienie: ${permission}`);
    }
    this.viewContainer.clear();
    if (this.auth.hasPermission(permission)) {
      this.viewContainer.createEmbeddedView(this.templateRef);
    }
  }
}
