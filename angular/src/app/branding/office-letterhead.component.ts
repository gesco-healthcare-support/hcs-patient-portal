import {
  ChangeDetectionStrategy,
  Component,
  Input,
  OnChanges,
  OnInit,
  SimpleChanges,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { Observable } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { IconComponent } from '../shared/ui/icon/icon.component';
import type { OfficeLetterheadDto, UpdateOfficeLetterheadInput } from '../proxy/branding/models';
import { OfficeLetterheadService } from '../proxy/branding/office-letterhead.service';
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
 *
 * 2026-10-21: with `officeId` set it edits THAT office from the host branding page, through the
 * host-only `api/app/branding/offices/{officeId}/letterhead` (a host Staff Supervisor or IT Admin
 * holds Branding there, but not inside an office). Without it, the current office, as before.
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
export class OfficeLetterheadComponent implements OnInit, OnChanges {
  private readonly service = inject(OfficeLetterheadService);
  private readonly rest = inject(RestService);
  private readonly toaster = inject(ToasterService);

  /** The office to edit from the host branding page; unset = the current office. */
  @Input() officeId?: string;

  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly defaults = signal<Pick<
    OfficeLetterheadDto,
    'defaultPhysicianName' | 'defaultLetterheadName' | 'defaultPracticeName'
  > | null>(null);
  protected form: LetterheadForm = emptyLetterheadForm();

  ngOnInit(): void {
    this.load();
  }

  /** The host page switches offices on one card; reload when it does. */
  ngOnChanges(changes: SimpleChanges): void {
    const change = changes['officeId'];
    if (change && !change.firstChange) {
      this.load();
    }
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
    this.write(input)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (dto) => {
          this.apply(dto);
          this.toaster.success('Packet letterhead saved.');
        },
        error: () => undefined,
      });
  }

  private load(): void {
    this.loading.set(true);
    this.read()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (dto) => this.apply(dto),
        error: () => undefined,
      });
  }

  private read(): Observable<OfficeLetterheadDto> {
    return this.officeId
      ? this.rest.request<void, OfficeLetterheadDto>(
          { method: 'GET', url: this.officeUrl(this.officeId) },
          { apiName: 'Default' },
        )
      : this.service.get();
  }

  private write(input: UpdateOfficeLetterheadInput): Observable<OfficeLetterheadDto> {
    return this.officeId
      ? this.rest.request<UpdateOfficeLetterheadInput, OfficeLetterheadDto>(
          { method: 'PUT', url: this.officeUrl(this.officeId), body: input },
          { apiName: 'Default' },
        )
      : this.service.update(input);
  }

  private officeUrl(officeId: string): string {
    return `/api/app/branding/offices/${encodeURIComponent(officeId)}/letterhead`;
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
