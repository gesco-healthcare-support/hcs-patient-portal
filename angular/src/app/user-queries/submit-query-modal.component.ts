import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  HostListener,
  Input,
  Output,
  inject,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { firstValueFrom } from 'rxjs';
import { IconComponent } from '../shared/ui/icon/icon.component';

/**
 * Submit-Query / Contact-Us modal -- the OLD "Help / Need Question?" popup,
 * restyled to the redesign's .ext-modal shell. Plain standalone modal (no
 * LeptonX abp-modal) so it matches the redesigned external pages. Collects a
 * required free-text question (max 500) plus an optional appointment
 * confirmation number, then POSTs to the user-queries endpoint via RestService
 * (mirrors the cancel-appointment modal, so no proxy regeneration is needed).
 *
 * Usage from parent:
 *   <app-submit-query-modal [(visible)]="submitQueryVisible" />
 */
@Component({
  selector: 'app-submit-query-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [FormsModule, IconComponent],
  templateUrl: './submit-query-modal.component.html',
  styleUrl: './submit-query-modal.component.scss',
})
export class SubmitQueryModalComponent {
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();

  private readonly restService = inject(RestService);
  private readonly toaster = inject(ToasterService);

  message = '';
  requestConfirmationNumber = '';
  isBusy = false;

  readonly maxMessageLength = 500;
  readonly maxConfirmationNumberLength = 50;

  get canSubmit(): boolean {
    const trimmed = this.message.trim().length;
    return !this.isBusy && trimmed > 0 && this.message.length <= this.maxMessageLength;
  }

  /** Escape closes the modal unless a submit is in flight. */
  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.visible && !this.isBusy) {
      this.setVisible(false);
    }
  }

  setVisible(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
    if (!value) {
      this.message = '';
      this.requestConfirmationNumber = '';
      this.isBusy = false;
    }
  }

  async submit(): Promise<void> {
    if (!this.canSubmit) {
      return;
    }
    this.isBusy = true;
    try {
      await firstValueFrom(
        this.restService.request<
          { message: string; requestConfirmationNumber: string | null },
          void
        >(
          {
            method: 'POST',
            url: '/api/app/user-queries',
            body: {
              message: this.message.trim(),
              requestConfirmationNumber: this.requestConfirmationNumber.trim() || null,
            },
          },
          { apiName: 'Default' },
        ),
      );
      this.toaster.success('Your query has been sent to the clinic team.');
      this.setVisible(false);
    } catch {
      // ABP default error handler renders the BusinessException toast.
      this.isBusy = false;
    }
  }
}
