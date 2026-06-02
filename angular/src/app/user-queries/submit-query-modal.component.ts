import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe, RestService } from '@abp/ng.core';
import {
  ModalComponent,
  ModalCloseDirective,
  ButtonComponent,
  ToasterService,
} from '@abp/ng.theme.shared';
import { firstValueFrom } from 'rxjs';

/**
 * Submit-Query / Contact-Us modal -- the OLD "Help / Need Question?" popup.
 * Collects a required free-text question (max 500) plus an optional
 * appointment confirmation number, then POSTs to the user-queries endpoint.
 * Uses RestService directly (mirroring the cancel-appointment modal) so the
 * new endpoint needs no proxy regeneration.
 *
 * Usage from parent:
 *   <app-submit-query-modal [(visible)]="submitQueryVisible"></app-submit-query-modal>
 */
@Component({
  selector: 'app-submit-query-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    ModalComponent,
    ModalCloseDirective,
    ButtonComponent,
  ],
  templateUrl: './submit-query-modal.component.html',
  styles: [],
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
      this.toaster.success('Your question has been sent');
      this.setVisible(false);
    } catch {
      // ABP default error handler renders the BusinessException toast.
      this.isBusy = false;
    }
  }
}
