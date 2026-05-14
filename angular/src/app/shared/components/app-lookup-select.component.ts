import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LookupSelectComponent } from '@volo/abp.commercial.ng.ui';
import { LocalizationPipe } from '@abp/ng.core';

/**
 * 2026-05-14 (BUG-007 fix) -- local wrapper around ABP's
 * `<abp-lookup-select>` that adds the missing `cdRef.markForCheck()`
 * after the lookup-API subscribe sets `this.datas`. Forked from
 * `@volo/abp.commercial.ng.ui` 10.0.2.
 *
 * Why this exists:
 *  - ABP's `LookupSelectComponent.get()` does
 *      `this.getFn(this.pageQuery).subscribe(({ items }) => {
 *         this.datas = items;
 *      });`
 *    without any `cdRef.markForCheck()`. The component itself is
 *    `Default` change-detection, but when its parent is OnPush and the
 *    parent never receives a post-mount input mutation, the parent
 *    (and therefore this component as its descendant) is skipped on
 *    the next ApplicationRef.tick() pass. The `@for (data of datas)`
 *    binding never re-evaluates and the dropdown stays empty.
 *  - This bit the appointment-add 7-section decomposition (PR #121):
 *    Schedule + Attorney sections (Default-CD before the decomposition,
 *    OnPush after) lost their lookup-select rendering. Patient
 *    Demographics + Employer Details worked only because they happened
 *    to receive a mutable input (patientListCache / patientLoadMessage)
 *    that triggered dirty-check during the same NgZone task as the
 *    subscribe -- not a stable guarantee, and a page-reload race could
 *    flip which sections rendered.
 *
 * The fix is one line: call `this.cdRef.markForCheck()` immediately
 * after assigning `datas`. That marks the parent path dirty so the
 * next CD pass re-renders the @for. Behaviour is identical to ABP's
 * component for every other input/output/ControlValueAccessor concern.
 *
 * Usage:
 *   <app-lookup-select cid="..." formControlName="..." [getFn]="..." />
 *
 * Template note: Angular components cannot inherit templates from a
 * base class, so the entire `<select>` markup is copied verbatim from
 * ABP's component. If ABP changes the lookup-select template in a
 * future version, audit this template alongside the upgrade.
 */
@Component({
  selector: 'app-lookup-select',
  standalone: true,
  imports: [FormsModule, LocalizationPipe],
  template: `
    <select
      [id]="cid"
      class="form-select form-control"
      [(ngModel)]="value"
      [disabled]="disabled"
      [class.input-validation-error]="isInvalid"
    >
      <option [ngValue]="emptyOption.value">{{ emptyOption.label | abpLocalization }}</option>
      @for (data of datas; track $index) {
        <option [ngValue]="data[lookupIdProp]">
          {{ data[lookupNameProp] }}
        </option>
      }
    </select>
  `,
})
export class AppLookupSelectComponent extends LookupSelectComponent {
  override get() {
    this.getFn(this.pageQuery).subscribe(({ items }) => {
      this.datas = items ?? [];
      // `cdRef` is inherited from AbstractNgModelComponent. Without
      // markForCheck the @for never re-evaluates when this component
      // sits under an OnPush parent that hasn't been marked dirty by
      // an input change.
      this.cdRef.markForCheck();
    });
  }
}
