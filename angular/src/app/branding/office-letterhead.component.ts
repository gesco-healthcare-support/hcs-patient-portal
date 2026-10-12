import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { finalize } from 'rxjs/operators';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { OfficeLetterheadDto, OfficeLetterheadService } from './office-letterhead.service';
import {
  LetterheadForm,
  emptyLetterheadForm,
  letterheadFormFromDto,
  letterheadInputFromForm,
  parseFee,
} from './office-letterhead.util';

/**
 * 2026-10-09 (walkthrough Q5) -- the packet letterhead card on the in-office branding page:
 * what this office's generated packets print as its identity (letterhead, physician,
 * practice name, address, phone, fax, records addresses, missed-appointment charge).
 *
 * Every field is optional. A blank one prints the default shown as its placeholder --
 * derived from the doctor entered when the practice was created -- so a new office's
 * packets are correct before anyone opens this card. Lines with no value (no fax, no fee)
 * are left off the packets rather than printed empty.
 */
@Component({
  selector: 'app-office-letterhead',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, IconComponent],
  templateUrl: './office-letterhead.component.html',
  styles: `
    .ra-card {
      max-width: 680px;
      margin: 18px auto 0;
    }
    .ol-preview {
      margin-bottom: 16px;
      padding: 12px 14px;
      border: 1px solid var(--border, #e6ebf2);
      border-radius: var(--r-md, 11px);
      background: var(--n-25, #fafbfd);
    }
    .ol-preview__name {
      font-size: 17px;
      font-weight: 700;
    }
    .ol-preview__tag {
      font-size: 11px;
      letter-spacing: 0.3px;
    }
    .ol-hint {
      font-size: 12px;
      color: var(--n-500, #6b778c);
      margin-top: 4px;
    }
    .ol-actions {
      margin-top: 16px;
    }
  `,
})
export class OfficeLetterheadComponent {
  private readonly service = inject(OfficeLetterheadService);
  private readonly toaster = inject(ToasterService);

  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly defaults = signal<Pick<
    OfficeLetterheadDto,
    'defaultPhysicianName' | 'defaultLetterheadName' | 'defaultPracticeName'
  > | null>(null);
  protected form: LetterheadForm = emptyLetterheadForm();

  constructor() {
    this.service
      .get()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (dto) => this.apply(dto),
        error: () => undefined,
      });
  }

  /** True when the fee box holds something that is not a valid amount. */
  protected feeInvalid(): boolean {
    return parseFee(this.form.fee) === undefined;
  }

  /** The heading as it will print: what is typed, else the default. */
  protected previewName(): string {
    return this.form.text.letterheadName.trim() || this.defaults()?.defaultLetterheadName || '';
  }

  protected save(): void {
    const input = letterheadInputFromForm(this.form);
    if (this.busy() || !input) {
      return;
    }
    this.busy.set(true);
    this.service
      .update(input)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (dto) => {
          this.apply(dto);
          this.toaster.success('Packet letterhead saved.');
        },
        error: () => undefined,
      });
  }

  private apply(dto: OfficeLetterheadDto): void {
    this.form = letterheadFormFromDto(dto);
    this.defaults.set({
      defaultPhysicianName: dto.defaultPhysicianName,
      defaultLetterheadName: dto.defaultLetterheadName,
      defaultPracticeName: dto.defaultPracticeName,
    });
  }
}
