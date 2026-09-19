import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { FormBuilder } from '@angular/forms';
import { ChangeDetectorRef } from '@angular/core';
import { of } from 'rxjs';
import { EnvironmentService, PermissionService, RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { AppointmentReportComponent } from './appointment-report.component';
import { InternalWcabOfficesComponent } from '../wcab-offices/wcab-office/internal-wcab-offices.component';
import { LanguageManagementComponent } from '../languages/language-management.component';
import { AppointmentTypeService } from '../proxy/appointment-types';
import { LocationService } from '../proxy/locations';
import { WcabOfficeService } from '../proxy/wcab-offices/wcab-office.service';
import { ReportService } from '../proxy/reports';
import { LanguageService } from '@volo/abp.ng.language-management/proxy';
import { AppointmentDocumentsComponent } from '../appointment-documents/appointment-documents.component';
import { AppointmentDocumentService } from '../proxy/appointment-documents/appointment-document.service';
import { AppointmentDocumentUrls } from '../appointment-documents/appointment-document-urls';

/**
 * #628 -- four overlays across these components could be dismissed only with a
 * mouse. Sonar's MouseEventWithoutKeyboardEquivalentCheck reports the scrim and
 * click-away divs; the fix is a document-level Escape handler, because a div is
 * not focusable and a tabindex would put a tab stop on a decorative overlay.
 *
 * Two properties matter and neither is "it closes":
 *
 *  - a handler bound to `document` fires on EVERY Escape in the app, so it must
 *    be inert when its overlay is shut
 *  - it must route through the existing close method, so the `if (!isBusy())`
 *    guard those methods already carry is inherited. Escape must not be able to
 *    abandon a save that the backdrop click would refuse to abandon.
 */
const STUBS = [
  FormBuilder,
  { provide: Router, useValue: { navigate: () => undefined, navigateByUrl: () => undefined } },
  { provide: HttpClient, useValue: { get: () => of(null), post: () => of(null) } },
  {
    provide: ChangeDetectorRef,
    useValue: { markForCheck: () => undefined, detectChanges: () => undefined },
  },
  { provide: RestService, useValue: { request: () => of(null) } },
  { provide: EnvironmentService, useValue: { getApiUrl: () => '' } },
  { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
  { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
  {
    provide: AppointmentTypeService,
    useValue: { getList: () => of({ items: [], totalCount: 0 }) },
  },
  { provide: LocationService, useValue: { getList: () => of({ items: [], totalCount: 0 }) } },
  { provide: WcabOfficeService, useValue: { getList: () => of({ items: [], totalCount: 0 }) } },
  { provide: ReportService, useValue: { getReport: () => of({ items: [], totalCount: 0 }) } },
  { provide: LanguageService, useValue: { getAllList: () => of({ items: [] }) } },
  {
    provide: AppointmentDocumentService,
    useValue: { getList: () => of({ items: [], totalCount: 0 }) },
  },
  { provide: AppointmentDocumentUrls, useValue: { download: () => '', preview: () => '' } },
];

describe('Escape closes the mouse-only overlays (#628)', () => {
  afterEach(() => TestBed.resetTestingModule());

  function make<T>(ctor: new () => T, extra: unknown[] = []): T {
    TestBed.configureTestingModule({ providers: [...STUBS, ...(extra as never[])] });
    return TestBed.runInInjectionContext(() => new ctor());
  }

  describe('report column picker', () => {
    interface Probe {
      colsOpen: boolean;
      onEscapeKey(): void;
    }

    it('closes an open column picker', () => {
      const c = make(AppointmentReportComponent) as unknown as Probe;
      c.colsOpen = true;
      c.onEscapeKey();
      expect(c.colsOpen).toBeFalse();
    });

    it('is inert when the picker is closed', () => {
      const c = make(AppointmentReportComponent) as unknown as Probe;
      c.colsOpen = false;
      expect(() => c.onEscapeKey()).not.toThrow();
      expect(c.colsOpen).toBeFalse();
    });
  });

  describe('WCAB offices, which has two overlays', () => {
    interface Probe {
      form: { set(v: unknown): void; (): unknown };
      confirmDelete: { set(v: unknown): void; (): unknown };
      isBusy: { set(v: boolean): void };
      onEscapeKey(): void;
    }

    function wcab(): Probe {
      return make(InternalWcabOfficesComponent) as unknown as Probe;
    }

    it('closes the edit modal when only it is open', () => {
      const c = wcab();
      c.form.set({ name: 'Fresno' });
      c.onEscapeKey();
      expect(c.form()).toBeNull();
    });

    it('closes the delete confirmation when only it is open', () => {
      const c = wcab();
      c.confirmDelete.set({ wcabOffice: { id: 'w1' } });
      c.onEscapeKey();
      expect(c.confirmDelete()).toBeNull();
    });

    /**
     * The confirmation renders after the modal, so it is on top. Escape must
     * take the top one -- closing the form underneath would leave the prompt
     * orphaned over nothing.
     */
    it('takes the delete confirmation first when both are open', () => {
      const c = wcab();
      c.form.set({ name: 'Fresno' });
      c.confirmDelete.set({ wcabOffice: { id: 'w1' } });

      c.onEscapeKey();

      expect(c.confirmDelete()).toBeNull();
      expect(c.form()).not.toBeNull();
    });

    /** Routed through cancelDelete/closeModal, so their isBusy guard applies. */
    it('refuses to close while a save is in flight', () => {
      const c = wcab();
      c.form.set({ name: 'Fresno' });
      c.isBusy.set(true);

      c.onEscapeKey();

      expect(c.form()).not.toBeNull();
    });

    it('is inert when neither overlay is open', () => {
      const c = wcab();
      expect(() => c.onEscapeKey()).not.toThrow();
      expect(c.form()).toBeNull();
      expect(c.confirmDelete()).toBeNull();
    });
  });

  describe('language edit modal', () => {
    interface Probe {
      editing: { set(v: string | null): void; (): string | null };
      isBusy: { set(v: boolean): void };
      onEscapeKey(): void;
    }

    it('closes the modal when one is being edited', () => {
      const c = make(LanguageManagementComponent) as unknown as Probe;
      c.editing.set('lang-1');
      c.onEscapeKey();
      expect(c.editing()).toBeNull();
    });

    it('refuses to close while a save is in flight', () => {
      const c = make(LanguageManagementComponent) as unknown as Probe;
      c.editing.set('lang-1');
      c.isBusy.set(true);
      c.onEscapeKey();
      expect(c.editing()).toBe('lang-1');
    });

    it('is inert when no modal is open', () => {
      const c = make(LanguageManagementComponent) as unknown as Probe;
      expect(() => c.onEscapeKey()).not.toThrow();
      expect(c.editing()).toBeNull();
    });
  });
  describe('document reject modal', () => {
    interface Probe {
      rejectingDoc: unknown;
      rejectionReason: string;
      isSubmittingReject: boolean;
      onEscapeKey(): void;
    }

    function docs(): Probe {
      return make(AppointmentDocumentsComponent) as unknown as Probe;
    }

    /**
     * closeRejectModal also clears the typed reason and the submitting flag.
     * Escape routes through it rather than nulling rejectingDoc directly, so a
     * reopened modal cannot show the previous rejection text.
     */
    it('closes the reject modal and clears its state', () => {
      const c = docs();
      c.rejectingDoc = { id: 'doc-1' };
      c.rejectionReason = 'typed but abandoned';
      c.isSubmittingReject = true;

      c.onEscapeKey();

      expect(c.rejectingDoc).toBeNull();
      expect(c.rejectionReason).toBe('');
      expect(c.isSubmittingReject).toBeFalse();
    });

    it('is inert when no document is being rejected', () => {
      const c = docs();
      c.rejectingDoc = null;

      expect(() => c.onEscapeKey()).not.toThrow();
      expect(c.rejectingDoc).toBeNull();
    });
  });
});
