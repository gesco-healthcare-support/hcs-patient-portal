import {
  ChangeDetectionStrategy,
  Component,
  forwardRef,
  inject,
  Input,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { ConfigStateService, LocalizationPipe } from '@abp/ng.core';
import { PatientService } from '../../proxy/patients/patient.service';

/**
 * F1 / Design B (2026-05-29) -- SSN entry + reveal control.
 *
 * Standard API payloads now carry only the masked last-4 of an SSN; the full
 * value is served solely by the audited reveal endpoint
 * (PatientService.getFullSsn). This component is the single UI surface for SSN
 * on every form, and it enforces three rules:
 *
 *  1. NEVER pre-filled. The editable field starts empty. Parents bind it via
 *     formControlName; an empty submit means "leave the stored SSN unchanged"
 *     (the backend PatientManager.UpdateAsync rule). The form value is the raw
 *     digit string the user types, or null when untouched.
 *  2. Mask-on-type. While the user types, digits are visible; after ~1.2s idle
 *     (or on blur) the entry redacts to ***-**-LAST4. An eye toggle re-reveals
 *     what was typed. Copy/cut of the entered value is blocked; paste is allowed.
 *  3. Reveal-on-file. When a patientId + a masked on-file value are supplied and
 *     the caller is internal or the record owner, an eye button fetches the full
 *     stored SSN from the audited endpoint and shows it read-only. The button is
 *     hidden for everyone else (the server also re-checks and returns 403).
 *
 * Roles/ownership are read from ABP's ConfigStateService current user, mirroring
 * the server-side SsnRevealAccess predicate (internal role OR record owner).
 */
@Component({
  selector: 'app-ssn-input',
  standalone: true,
  imports: [CommonModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SsnInputComponent),
      multi: true,
    },
  ],
  template: `
    <!-- I13 (2026-06-08): the on-file masked SSN is shown INSIDE the box below
         (not on a separate line above). The box's form value stays empty until
         the user types a replacement (empty submit = leave stored SSN
         unchanged); the eye button performs the audited on-file reveal while no
         new value has been entered. Mask-on-type + copy blocked unchanged. -->
    <!-- Entry / change field: shows the on-file value until the user types. -->
    <div class="input-group">
      <input
        #entry
        type="text"
        class="form-control"
        [class.is-invalid]="invalid"
        [value]="boxDisplay()"
        [attr.placeholder]="placeholder"
        [disabled]="disabled"
        maxlength="11"
        autocomplete="off"
        inputmode="numeric"
        (focus)="onFocus(entry)"
        (blur)="onBlur()"
        (keydown)="onKeydown(entry)"
        (input)="onInput(entry)"
        (copy)="block($event)"
        (cut)="block($event)"
      />
      <button
        type="button"
        class="btn btn-outline-secondary"
        (click)="onEyeClick(entry)"
        [disabled]="eyeDisabled()"
        [attr.aria-label]="eyeAriaLabel()"
      >
        <i class="fa" [class.fa-eye]="eyeClosed()" [class.fa-eye-slash]="!eyeClosed()"></i>
      </button>
    </div>
  `,
})
export class SsnInputComponent implements ControlValueAccessor {
  /** Patient whose stored SSN the on-file reveal endpoint targets. */
  @Input() patientId?: string | null;
  /** The patient's IdentityUserId, for the record-owner check. */
  @Input() patientIdentityUserId?: string | null;
  /** The masked on-file value from the DTO (e.g. ***-**-1234) to display. */
  @Input() currentMaskedSsn?: string | null;
  /** Mirrors the host input's invalid styling. */
  @Input() invalid = false;
  @Input() placeholder = '';

  private readonly patientService = inject(PatientService);
  private readonly configState = inject(ConfigStateService);

  // Entry state. entryDigits holds the raw value the user typed (the form
  // value); entryHidden drives the redacted display.
  readonly entryDigits = signal('');
  readonly entryHidden = signal(true);
  private focused = false;
  private idleTimer: ReturnType<typeof setTimeout> | null = null;

  // On-file reveal state.
  readonly onFileRevealed = signal(false);
  readonly onFileLoading = signal(false);
  private readonly onFileFull = signal<string | null>(null);

  disabled = false;
  private onChange: (value: string | null) => void = () => {};
  private onTouched: () => void = () => {};

  private static readonly IdleMs = 1200;
  // I14 (2026-06-08): bullet (codepoint U+2022) used to redact hidden SSN
  // digits. Built via fromCharCode to keep the source ASCII-only.
  private static readonly RedactionDot = String.fromCharCode(0x2022);

  // ----- display computeds -----

  entryDisplay(): string {
    const digits = this.entryDigits();
    if (!digits) {
      return '';
    }
    if (this.entryHidden()) {
      return SsnInputComponent.mask(digits);
    }
    // While focused show the raw digits (no cursor-shifting separators); when
    // revealed but not focused show the grouped form.
    return this.focused ? digits : SsnInputComponent.format(digits);
  }

  /**
   * I13 (2026-06-08): value shown in the single SSN box. While focused or after
   * the user types, show the entry value. Otherwise, when a stored value exists,
   * show it inside the box (masked, or the revealed full value). The form value
   * stays null until the user types -- an untouched box means "leave unchanged".
   */
  boxDisplay(): string {
    if (this.focused || this.entryDigits()) {
      return this.entryDisplay();
    }
    return this.showOnFile() ? this.onFileDisplay() : '';
  }

  showOnFile(): boolean {
    return !!this.patientId && !!this.currentMaskedSsn;
  }

  onFileDisplay(): string {
    const full = this.onFileFull();
    if (this.onFileRevealed() && full) {
      return SsnInputComponent.format(full);
    }
    // I14 (2026-06-08): the DTO masks the stored value with '*' (***-**-1234);
    // render the hidden digits as bullet circles (codepoint U+2022) for a
    // cleaner redaction. The escape keeps the source ASCII-only.
    return (this.currentMaskedSsn ?? '').replace(/\*/g, SsnInputComponent.RedactionDot);
  }

  canReveal(): boolean {
    const user = this.currentUser();
    if (!user) {
      return false;
    }
    if (SsnInputComponent.isInternal(user.roles)) {
      return true;
    }
    return !!user.id && !!this.patientIdentityUserId && user.id === this.patientIdentityUserId;
  }

  // ----- entry handlers -----

  onFocus(el: HTMLInputElement): void {
    this.focused = true;
    this.entryHidden.set(false);
    // I13: when the box is showing the on-file masked value (no new SSN typed
    // yet), clear the field on focus so the mask is never read back as typed
    // input. The form value stays null until the user actually types.
    if (!this.entryDigits()) {
      el.value = '';
    }
    this.restartIdle();
  }

  onBlur(): void {
    this.focused = false;
    this.clearIdle();
    this.entryHidden.set(true);
    this.onTouched();
  }

  onKeydown(el: HTMLInputElement): void {
    // If the field is currently showing the masked form, restore the raw digits
    // before the keystroke lands so editing never corrupts the mask string.
    if (this.entryHidden()) {
      this.entryHidden.set(false);
      el.value = this.entryDigits();
      const end = el.value.length;
      el.setSelectionRange(end, end);
    }
    this.restartIdle();
  }

  onInput(el: HTMLInputElement): void {
    const digits = (el.value.match(/\d/g) ?? []).join('').slice(0, 9);
    this.entryDigits.set(digits);
    this.entryHidden.set(false);
    // Reflect the cleaned digits back so stray separators cannot accumulate.
    if (el.value !== digits) {
      el.value = digits;
    }
    this.onChange(digits.length ? digits : null);
    this.restartIdle();
  }

  toggleEntryReveal(el: HTMLInputElement): void {
    const hide = !this.entryHidden();
    this.entryHidden.set(hide);
    if (!hide) {
      el.value = this.focused ? this.entryDigits() : SsnInputComponent.format(this.entryDigits());
    }
  }

  // ----- I13: single eye button drives entry-reveal or on-file-reveal -----

  /**
   * Reveals the typed value once the user has typed; otherwise performs the
   * audited on-file reveal of the stored SSN.
   */
  onEyeClick(el: HTMLInputElement): void {
    if (this.entryDigits()) {
      this.toggleEntryReveal(el);
    } else if (this.showOnFile()) {
      this.toggleOnFile();
    }
  }

  /** True when the shown value is currently redacted (fa-eye icon). */
  eyeClosed(): boolean {
    return this.entryDigits() ? this.entryHidden() : !this.onFileRevealed();
  }

  /**
   * Revealing a typed entry follows the input's disabled state. Revealing the
   * on-file value is allowed even on a read-only (disabled) form -- mirroring
   * the previous standalone reveal button -- as long as the user may reveal and
   * no fetch is in flight.
   */
  eyeDisabled(): boolean {
    if (this.entryDigits()) {
      return this.disabled;
    }
    return !(this.showOnFile() && this.canReveal()) || this.onFileLoading();
  }

  eyeAriaLabel(): string {
    if (this.entryDigits()) {
      return this.entryHidden() ? 'Show entered SSN' : 'Hide entered SSN';
    }
    return this.onFileRevealed() ? 'Hide SSN on file' : 'Reveal SSN on file';
  }

  block(event: Event): void {
    event.preventDefault();
  }

  // ----- on-file reveal -----

  toggleOnFile(): void {
    if (this.onFileRevealed()) {
      this.onFileRevealed.set(false);
      return;
    }
    if (this.onFileFull() !== null) {
      this.onFileRevealed.set(true);
      return;
    }
    if (!this.patientId) {
      return;
    }
    this.onFileLoading.set(true);
    this.patientService.getFullSsn(this.patientId).subscribe({
      next: (dto) => {
        this.onFileFull.set(dto?.socialSecurityNumber ?? '');
        this.onFileRevealed.set(true);
        this.onFileLoading.set(false);
      },
      error: () => this.onFileLoading.set(false),
    });
  }

  // ----- ControlValueAccessor -----

  writeValue(value: string | null): void {
    const digits = value ? (value.match(/\d/g) ?? []).join('').slice(0, 9) : '';
    this.entryDigits.set(digits);
    this.entryHidden.set(true);
  }

  registerOnChange(fn: (value: string | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
  }

  // ----- helpers -----

  private restartIdle(): void {
    this.clearIdle();
    this.idleTimer = setTimeout(() => {
      if (this.focused) {
        this.entryHidden.set(true);
      }
    }, SsnInputComponent.IdleMs);
  }

  private clearIdle(): void {
    if (this.idleTimer) {
      clearTimeout(this.idleTimer);
      this.idleTimer = null;
    }
  }

  private currentUser(): { id?: string; roles?: string[] } | null {
    return (this.configState.getOne('currentUser') as { id?: string; roles?: string[] }) ?? null;
  }

  private static readonly InternalRoles = [
    'admin',
    'clinic staff',
    'staff supervisor',
    'it admin',
    'doctor',
  ];

  private static isInternal(roles?: string[]): boolean {
    return (roles ?? []).some((r) =>
      SsnInputComponent.InternalRoles.includes((r ?? '').trim().toLowerCase()),
    );
  }

  private static mask(digits: string): string {
    if (!digits) {
      return '';
    }
    // I14 (2026-06-08): bullet circles (codepoint U+2022) instead of asterisks
    // for a cleaner redaction. The escape renders as a dot and keeps the
    // source ASCII-only.
    const dot = SsnInputComponent.RedactionDot;
    return digits.length < 4
      ? dot.repeat(digits.length)
      : `${dot}${dot}${dot}-${dot}${dot}-${digits.slice(-4)}`;
  }

  private static format(digits: string): string {
    if (digits.length <= 3) {
      return digits;
    }
    if (digits.length <= 5) {
      return `${digits.slice(0, 3)}-${digits.slice(3)}`;
    }
    return `${digits.slice(0, 3)}-${digits.slice(3, 5)}-${digits.slice(5, 9)}`;
  }
}
