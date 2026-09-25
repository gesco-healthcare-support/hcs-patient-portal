import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { BrandingService } from '../../branding/branding.service';

@Component({
  selector: 'app-top-header-navbar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './top-header-navbar.component.html',
  styleUrl: './top-header-navbar.component.scss',
})
export class TopHeaderNavbarComponent {
  /** Per-office branding (logo + display name); overrides the static logo when present. */
  protected readonly branding = inject(BrandingService);
  @Input() tenantName = '';
  @Input() userName = '';
  @Input() roleName = '';
  @Input() showProfile = true;
  @Input() showHelp = true;
  @Input() showLogout = true;

  @Output() profileClick = new EventEmitter<void>();
  @Output() helpClick = new EventEmitter<void>();
  @Output() logoutClick = new EventEmitter<void>();

  onProfileClick(): void {
    this.profileClick.emit();
  }

  onHelpClick(): void {
    this.helpClick.emit();
  }

  onLogoutClick(): void {
    this.logoutClick.emit();
  }
}
