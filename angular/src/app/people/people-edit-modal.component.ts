import {
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
  imports: [CommonModule, FormsModule, IconComponent],
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
