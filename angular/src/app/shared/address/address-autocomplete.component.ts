import {
  ChangeDetectionStrategy,
  Component,
  inject,
  Input,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Subject, Subscription, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { PatientService } from '../../proxy/patients/patient.service';
import { AddressSuggestion, AddressValidationProvider } from './address-validation.provider';
import { resolveStateId, StateLookupOption } from './state-resolver';

/**
 * Maps the abstract address pieces to a booking-form group's control names.
 * `suite` is optional (only patient "Unit #" + insurance/CE "STE" have one);
 * `state` names the StateId control (a `<select>` keyed by GUID).
 */
export interface AddressFieldMap {
  street: string;
  suite?: string;
  city: string;
  state: string;
  zip: string;
}

/**
 * F2 / address validation (2026-05-29) -- reusable street-field autocomplete.
 * Renders the street input (bound to the existing reactive control via
 * `[group]` + `fields.street`) and a suggestions dropdown from the injected
 * vendor-neutral `AddressValidationProvider`. Picking a suggestion patches the
 * whole group -- street/suite/city/zip directly, and state via
 * `resolveStateId` against the shared State lookup (loaded once here, so each
 * of the six groups only needs `[group]` + `[fields]`).
 *
 * Degrades silently: provider errors yield no suggestions and never block typing.
 */
@Component({
  selector: 'app-address-autocomplete',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="position-relative">
      <input
        type="text"
        class="form-control"
        [formControl]="streetControl"
        [attr.placeholder]="placeholder"
        autocomplete="off"
        (input)="onType($event)"
        (focus)="onFocus()"
        (blur)="onBlur()"
      />
      @if (open() && suggestions().length) {
        <ul
          class="list-group position-absolute w-100 shadow-sm"
          style="z-index: 1056; max-height: 240px; overflow-y: auto"
        >
          @for (s of suggestions(); track s.text) {
            <li
              class="list-group-item list-group-item-action"
              style="cursor: pointer"
              (mousedown)="select(s)"
            >
              {{ s.text }}
            </li>
          }
        </ul>
      }
    </div>
  `,
})
export class AddressAutocompleteComponent implements OnInit, OnDestroy {
  /** The booking-form group whose address controls this fills. */
  @Input({ required: true }) group!: FormGroup;
  /** Control-name map for this group's address pieces. */
  @Input({ required: true }) fields!: AddressFieldMap;
  @Input() placeholder = '';

  private readonly provider = inject(AddressValidationProvider);
  private readonly patientService = inject(PatientService);

  readonly suggestions = signal<AddressSuggestion[]>([]);
  readonly open = signal(false);
  private stateLookup: StateLookupOption[] = [];

  private readonly query$ = new Subject<string>();
  private readonly subs = new Subscription();

  ngOnInit(): void {
    this.subs.add(
      this.query$
        .pipe(
          debounceTime(250),
          distinctUntilChanged(),
          switchMap((q) => this.provider.autocomplete(q)),
        )
        .subscribe((results) => {
          this.suggestions.set(results ?? []);
          this.open.set((results ?? []).length > 0);
        }),
    );

    // Load the shared State lookup once so picks can resolve a state name/code
    // to the StateId the <select> is keyed by. Same endpoint every group uses.
    this.subs.add(
      this.patientService
        .getStateLookup({ maxResultCount: 1000, skipCount: 0, filter: '' })
        .subscribe((res) => {
          this.stateLookup = (res?.items ?? []).map((i) => ({
            id: String(i.id),
            name: i.displayName ?? '',
          }));
        }),
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  get streetControl(): FormControl {
    return this.group.get(this.fields.street) as FormControl;
  }

  onType(event: Event): void {
    const value = (event.target as HTMLInputElement).value ?? '';
    this.query$.next(value.trim());
  }

  onFocus(): void {
    if (this.suggestions().length) {
      this.open.set(true);
    }
  }

  onBlur(): void {
    // Defer so a suggestion's mousedown registers before the list hides.
    setTimeout(() => this.open.set(false), 150);
  }

  select(s: AddressSuggestion): void {
    const patch: Record<string, unknown> = {
      [this.fields.street]: s.street,
      [this.fields.city]: s.city,
      [this.fields.zip]: s.zip,
    };
    if (this.fields.suite && s.suite) {
      patch[this.fields.suite] = s.suite;
    }
    const stateId = resolveStateId(s.state, this.stateLookup);
    if (stateId) {
      patch[this.fields.state] = stateId;
    }
    this.group.patchValue(patch);
    this.group.markAsDirty();
    this.open.set(false);
    this.suggestions.set([]);
  }
}
