import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  Input,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  inject,
} from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { PagedResultDto } from '@abp/ng.core';
import { AppLookupSelectComponent } from '../../shared/components/app-lookup-select.component';
import type { LookupDto, LookupRequestDto } from '../../proxy/shared/models';
import { Observable, Subscription } from 'rxjs';

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
  imports: [CommonModule, ReactiveFormsModule, AppLookupSelectComponent],
  templateUrl: './appointment-add-attorney-section.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppointmentAddAttorneySectionComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) form!: FormGroup;
  @Input({ required: true }) role!: 'applicant' | 'defense';
  @Input({ required: true }) getStateLookup!: (
    input: LookupRequestDto,
  ) => Observable<PagedResultDto<LookupDto<string>>>;
  @Input() isReadOnly = false;
  /** BUG-044: when true, the section is required -- the "Include" toggle is
   * hidden and the body always renders. The parent keeps `{prefix}Enabled`
   * locked to true so the required validators stay applied. */
  @Input() mandatory = false;
  @Input() isFieldInvalid: (name: string) => boolean = () => false;

  // 2026-05-28 -- when the parent reverts the AA toggle from the
  // self-represented confirmation modal it uses
  // `setValue(true, { emitEvent: false })`. Even with emitEvent: true,
  // a programmatic form change does not mark this OnPush child for
  // check (the click that triggered the revert happened in ABP's
  // overlay, outside our component tree). We subscribe to the
  // `{prefix}Enabled` control's valueChanges and call markForCheck so
  // the @if guarding the card body re-evaluates after every flip,
  // regardless of where the change originated.
  private readonly cdr = inject(ChangeDetectorRef);
  private enabledSub?: Subscription;

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

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['form'] || changes['role']) {
      this.subscribeToEnabledChanges();
    }
  }

  ngOnDestroy(): void {
    this.enabledSub?.unsubscribe();
  }

  private subscribeToEnabledChanges(): void {
    this.enabledSub?.unsubscribe();
    const control = this.form?.get(this.prefix + 'Enabled');
    if (!control) return;
    this.enabledSub = control.valueChanges.subscribe(() => this.cdr.markForCheck());
  }
}
