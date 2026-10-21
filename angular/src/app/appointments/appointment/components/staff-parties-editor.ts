import { FormControl, FormGroup, Validators } from '@angular/forms';
import { RestService } from '@abp/ng.core';
import { firstValueFrom } from 'rxjs';

import type { AppointmentDto } from '../../../proxy/appointments/models';
import type { AppointmentClaimExaminerDto } from '../../../proxy/appointment-claim-examiners/models';
import {
  buildStaffPartiesPayload,
  partiesPayloadProblem,
  type AppointmentPartiesUpdate,
  type ClaimExaminerFormValue,
} from './staff-parties.util';

export type RemovableParty = 'applicantAttorney' | 'defenseAttorney' | 'claimExaminer';

/**
 * The staff side of "Edit details" for the parties (2026-10-21): the claim
 * examiner editor, the remove/undo flags, and the save through
 * PUT /api/app/appointments/{id}/parties. A plain class rather than more code
 * in the detail component, so it is tested on its own.
 *
 * begin() captures the values the edit started from; only what changed since
 * then is sent (see staff-parties.util).
 */
export class StaffPartiesEditor {
  readonly claimExaminerForm = new FormGroup({
    name: new FormControl<string | null>(null, [Validators.maxLength(50)]),
    email: new FormControl<string | null>(null, [Validators.email, Validators.maxLength(255)]),
    phoneNumber: new FormControl<string | null>(null, [Validators.maxLength(20)]),
    fax: new FormControl<string | null>(null, [Validators.maxLength(15)]),
    suite: new FormControl<string | null>(null, [Validators.maxLength(255)]),
    street: new FormControl<string | null>(null, [Validators.maxLength(100)]),
    city: new FormControl<string | null>(null, [Validators.maxLength(50)]),
    stateId: new FormControl<string | null>(null),
    zip: new FormControl<string | null>(null, [Validators.maxLength(10)]),
  });

  removed: Record<RemovableParty, boolean> = {
    applicantAttorney: false,
    defenseAttorney: false,
    claimExaminer: false,
  };

  private initialForm: Record<string, unknown> = {};
  private initialClaimExaminer: ClaimExaminerFormValue = {};

  constructor(private readonly rest: RestService) {}

  begin(
    form: Record<string, unknown>,
    claimExaminer: AppointmentClaimExaminerDto | null | undefined,
  ): void {
    this.initialForm = { ...form };
    this.initialClaimExaminer = {
      name: claimExaminer?.name ?? null,
      email: claimExaminer?.email ?? null,
      phoneNumber: claimExaminer?.phoneNumber ?? null,
      fax: claimExaminer?.fax ?? null,
      suite: claimExaminer?.suite ?? null,
      street: claimExaminer?.street ?? null,
      city: claimExaminer?.city ?? null,
      stateId: claimExaminer?.stateId ?? null,
      zip: claimExaminer?.zip ?? null,
    };
    this.claimExaminerForm.reset(this.initialClaimExaminer);
    this.resetRemoved();
  }

  cancel(): void {
    this.claimExaminerForm.reset(this.initialClaimExaminer);
    this.resetRemoved();
  }

  remove(party: RemovableParty): void {
    this.removed[party] = true;
  }

  undo(party: RemovableParty): void {
    this.removed[party] = false;
  }

  /** True when the edit started with this party on the appointment (so Remove makes sense). */
  hadParty(party: RemovableParty): boolean {
    if (party === 'claimExaminer') {
      return Object.values(this.initialClaimExaminer).some((v) => v !== null && v !== '');
    }
    return Object.keys(this.initialForm).some(
      (k) => k.startsWith(party) && k !== `${party}Enabled` && !!this.initialForm[k],
    );
  }

  build(
    raw: Record<string, unknown>,
    applicantAttorneyId: string | null | undefined,
    defenseAttorneyId: string | null | undefined,
  ): { payload: AppointmentPartiesUpdate; hasChanges: boolean } {
    return buildStaffPartiesPayload({
      raw,
      initial: this.initialForm,
      claimExaminer: this.claimExaminerForm.getRawValue(),
      initialClaimExaminer: this.initialClaimExaminer,
      removed: this.removed,
      applicantAttorneyId,
      defenseAttorneyId,
    });
  }

  /** A message to show before saving anything, or null when the parties are fine. */
  problem(
    raw: Record<string, unknown>,
    applicantAttorneyId: string | null | undefined,
    defenseAttorneyId: string | null | undefined,
  ): string | null {
    if (this.claimExaminerForm.invalid) {
      return 'Check the claim examiner details: the email address is not valid.';
    }
    return partiesPayloadProblem(this.build(raw, applicantAttorneyId, defenseAttorneyId).payload);
  }

  /** Sends the changes; resolves to the saved appointment, or null when nothing changed. */
  async save(
    appointmentId: string,
    raw: Record<string, unknown>,
    applicantAttorneyId: string | null | undefined,
    defenseAttorneyId: string | null | undefined,
  ): Promise<{ appointment: AppointmentDto; payload: AppointmentPartiesUpdate } | null> {
    const { payload, hasChanges } = this.build(raw, applicantAttorneyId, defenseAttorneyId);
    if (!hasChanges) {
      return null;
    }
    const appointment = await firstValueFrom(
      this.rest.request<AppointmentPartiesUpdate, AppointmentDto>(
        { method: 'PUT', url: `/api/app/appointments/${appointmentId}/parties`, body: payload },
        { apiName: 'Default' },
      ),
    );
    return { appointment, payload };
  }

  /** The claim examiner to show after a save that sent `payload`. */
  claimExaminerAfterSave(
    current: AppointmentClaimExaminerDto | null | undefined,
    payload: AppointmentPartiesUpdate,
  ): AppointmentClaimExaminerDto | null | undefined {
    const change = payload.claimExaminer;
    if (!change) {
      return current;
    }
    if (change.remove) {
      return null;
    }
    return {
      ...(current ?? {}),
      name: change.name ?? null,
      email: change.email ?? null,
      phoneNumber: change.phoneNumber ?? null,
      fax: change.fax ?? null,
      suite: change.suite ?? null,
      street: change.street ?? null,
      city: change.city ?? null,
      stateId: change.stateId ?? null,
      zip: change.zip ?? null,
      isActive: true,
    } as AppointmentClaimExaminerDto;
  }

  private resetRemoved(): void {
    this.removed = { applicantAttorney: false, defenseAttorney: false, claimExaminer: false };
  }
}
