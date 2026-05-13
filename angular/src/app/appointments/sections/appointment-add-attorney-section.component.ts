import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { PagedResultDto } from '@abp/ng.core';
import { LookupSelectComponent } from '@volo/abp.commercial.ng.ui';
import type { LookupDto, LookupRequestDto } from '../../proxy/shared/models';
import { Observable } from 'rxjs';

/**
 * #121 phase T5 (2026-05-13) -- shared Applicant Attorney / Defense
 * Attorney section. The two cards in OLD's appointment-add.component.html
 * are 95% identical: same 11 fields, same Include toggle, same
 * `[readonly]` pattern when the booker holds that role, same state
 * lookup, parallel API endpoints. The asymmetries (formControlName
 * prefix, heading text, checkbox id, readonly condition) all derive
 * from a single `role` Input.
 *
 * State ownership:
 *   - parent  -> all 12 attorney FormControls (per role) live on the
 *                main `form` FormGroup. The parent's submit-time
 *                upsertApplicantAttorneyForAppointmentIfProvided +
 *                upsertDefenseAttorneyForAppointmentIfProvided still
 *                read the raw form values, so nothing changes about
 *                where the data lives.
 *   - parent  -> the section visibility decision
 *                (shouldShowApplicantAttorneySection /
 *                shouldShowDefenseAttorneySection); the parent wraps
 *                the `<app-appointment-add-attorney-section>` element
 *                in the corresponding @if.
 *   - child   -> template rendering only. No state, no methods, no
 *                side effects.
 *
 * Trade-off: this is a minimum-viable shared-template extraction. A
 * deeper future refactor could move the email-search / load-by-email /
 * on-select methods + the id + concurrencyStamp tracking into a
 * child-owned scope, but that requires @ViewChild plumbing for the
 * parent's submit-time reads. Out of scope for T5; revisit when
 * adding new attorney features.
 */
@Component({
  selector: 'app-appointment-add-attorney-section',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, LookupSelectComponent],
  templateUrl: './appointment-add-attorney-section.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppointmentAddAttorneySectionComponent {
  @Input({ required: true }) form!: FormGroup;
  @Input({ required: true }) role!: 'applicant' | 'defense';
  @Input({ required: true }) getStateLookup!: (
    input: LookupRequestDto,
  ) => Observable<PagedResultDto<LookupDto<string>>>;
  @Input() isReadOnly = false;
  @Input() isFieldInvalid: (name: string) => boolean = () => false;

  /** Field-name prefix used by every formControlName in this section's
   * template. `applicantAttorney` or `defenseAttorney`. */
  get prefix(): string {
    return this.role + 'Attorney';
  }

  /** Card heading -- "Applicant Attorney Details" or "Defense Attorney Details". */
  get headingText(): string {
    return this.role === 'applicant' ? 'Applicant Attorney Details' : 'Defense Attorney Details';
  }

  /** DOM id for the Include checkbox -- `applicant-attorney-enabled` etc. */
  get checkboxId(): string {
    return this.role + '-attorney-enabled';
  }

  /** abp-lookup-select cid -- preserves the OLD-parity ids so
   * Playwright + a11y selectors keep working unchanged. */
  get stateSelectCid(): string {
    return 'appointment-' + this.role + '-attorney-state-id';
  }
}
