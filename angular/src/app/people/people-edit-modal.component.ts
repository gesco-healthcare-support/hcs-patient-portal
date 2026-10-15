import {
  HostListener,
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { genderOptions } from '../proxy/enums/gender.enum';
import { phoneNumberTypeOptions } from '../proxy/enums/phone-number-type.enum';
import type { LookupOption } from './people-section.gateway';
import type { PeopleSection, PersonFormState } from './people.util';

import { PhoneNumberDirective } from '../shared/phone-number.directive';
/**
 * Shared create/edit modal for every People section. The visible fields switch
 * on the section: full demographics for patients, firm fields for attorneys
 * (which carry no email on their DTO), and email for claim examiners. The SSN
 * field is write-only -- it never shows the stored value and a blank leaves it
 * unchanged. Validation + persistence live in the parent; this emits the draft.
 */
@Component({
  selector: 'app-people-edit-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PhoneNumberDirective, CommonModule, FormsModule, IconComponent],
  templateUrl: './people-edit-modal.component.html',
})
export class PeopleEditModalComponent {
  @Input({ required: true }) section!: PeopleSection;
  @Input() states: LookupOption[] = [];
  @Input() languages: LookupOption[] = [];
  @Input() busy = false;

  @Input({ required: true }) set form(value: PersonFormState) {
    this.draft.set({ ...value });
  }

  @Output() save = new EventEmitter<PersonFormState>();
  @Output() cancelled = new EventEmitter<void>();

  /**
   * Sweep #636: the modal could be dismissed only by clicking its backdrop, so a keyboard-only
   * user could open it and not get out.
   *
   * <p>Guarded on `busy` for the same reason the locations and users hubs guard theirs: a save
   * already in flight must not be dismissable, or the user is left looking at a form that is
   * still writing. Sonar's finding on the backdrop stays regardless -- it cannot see a
   * document-level handler.</p>
   */
  @HostListener('document:keydown.escape')
  protected onEscapeKey(): void {
    if (!this.busy) {
      this.cancelled.emit();
    }
  }

  protected readonly draft = signal<PersonFormState>({} as PersonFormState);
  protected readonly genders = genderOptions;
  protected readonly phoneTypes = phoneNumberTypeOptions;

  /** Claim examiners + patients carry an email field; attorneys do not. */
  protected get showEmail(): boolean {
    return this.section.isPatient || this.section.key === 'ce';
  }

  protected patch(partial: Partial<PersonFormState>): void {
    this.draft.set({ ...this.draft(), ...partial });
  }

  protected onSave(): void {
    if (!this.busy) {
      this.save.emit(this.draft());
    }
  }
}
