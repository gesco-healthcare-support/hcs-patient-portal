import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { PermissionService } from '@abp/ng.core';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { CONFIG_RAIL_ITEMS, ConfigRailItem } from './cf-config.util';

/**
 * Shared Configuration-hub left rail (#3, 2026-06-19). Rendered by both the
 * Configuration hub (its five lookup sections) and the WCAB Offices page, so
 * WCAB lives inside the same hub shell rather than as a standalone page. Active
 * state follows the live route; each item is hidden unless its ABP policy is
 * granted, matching the route guards so a visible item never resolves to a 403.
 */
@Component({
  selector: 'app-config-rail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterLink, RouterLinkActive, IconComponent],
  template: `
    <nav class="cf-rail">
      @for (item of items; track item.route) {
        @if (canSee(item)) {
          <a
            class="cf-railitem"
            [routerLink]="[item.route]"
            routerLinkActive
            #rla="routerLinkActive"
            [attr.data-on]="rla.isActive ? 'true' : null"
          >
            <span class="i"><app-icon [name]="item.icon" [size]="16" /></span>
            {{ item.label }}
          </a>
        }
      }
    </nav>
  `,
})
export class ConfigRailComponent {
  private readonly permission = inject(PermissionService);
  protected readonly items = CONFIG_RAIL_ITEMS;

  protected canSee(item: ConfigRailItem): boolean {
    return this.permission.getGrantedPolicy(item.policy);
  }
}
