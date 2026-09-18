import { TestBed } from '@angular/core/testing';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { LanguageService } from '@volo/abp.ng.language-management/proxy';
import { LanguageManagementComponent } from './language-management.component';

/**
 * Languages list over the stock Volo LanguageService.
 *
 * THIS COMPONENT LOADS FROM ITS CONSTRUCTOR (language-management.component.ts:48), so every
 * spy must be armed BEFORE the component is created, not after. That is why `probe()` is
 * called inside each test rather than in a beforeEach: a test that needs a failing list has
 * to change the spy first. Tranche 6 produced seven failures from exactly this mistake, with
 * the fixture reset in a create() helper after the test had armed it.
 *
 * DELIBERATELY NOT COVERED HERE: the Escape handler, which reports/overlay-escape-keys.spec.ts
 * already asserts for this component under #628.
 *
 * Create-versus-update is asserted on the ARGUMENTS. Both produce an identical success.
 */
describe('LanguageManagementComponent', () => {
  interface Signal<T> {
    (): T;
    set(value: T): void;
  }
  interface Probe {
    rows: Signal<unknown[]>;
    loading: Signal<boolean>;
    isBusy: Signal<boolean>;
    editing: Signal<string | null>;
    draft: Signal<Record<string, unknown> | null>;
    canManage: Signal<boolean>;
    flagLabel(culture: string | null | undefined): string;
    openNew(): void;
    openEdit(row: unknown): void;
    patchDraft(partial: Record<string, unknown>): void;
    close(): void;
    save(): void;
    setDefault(row: unknown): void;
    remove(row: unknown): void;
  }

  let getAllList: jasmine.Spy;
  let create: jasmine.Spy;
  let update: jasmine.Spy;
  let setAsDefault: jasmine.Spy;
  let remove: jasmine.Spy;
  let getGrantedPolicy: jasmine.Spy;
  let warn: jasmine.Spy;
  let success: jasmine.Spy;

  const english = {
    id: 'lang-en',
    displayName: 'English',
    cultureName: 'en',
    uiCultureName: 'en',
    flagIcon: 'flag-en',
    isEnabled: true,
    isDefaultLanguage: false,
    concurrencyStamp: 'stamp-en',
  };

  beforeEach(() => {
    getAllList = jasmine.createSpy('getAllList').and.returnValue(of({ items: [english] }));
    create = jasmine.createSpy('create').and.returnValue(of(english));
    update = jasmine.createSpy('update').and.returnValue(of(english));
    setAsDefault = jasmine.createSpy('setAsDefault').and.returnValue(of(english));
    remove = jasmine.createSpy('delete').and.returnValue(of(undefined));
    getGrantedPolicy = jasmine.createSpy('getGrantedPolicy').and.returnValue(true);
    warn = jasmine.createSpy('warn');
    success = jasmine.createSpy('success');

    TestBed.configureTestingModule({
      imports: [LanguageManagementComponent],
      providers: [
        {
          provide: LanguageService,
          useValue: { getAllList, create, update, setAsDefault, delete: remove },
        },
        { provide: PermissionService, useValue: { getGrantedPolicy } },
        { provide: ToasterService, useValue: { warn, success, error: () => undefined } },
      ],
    });
  });

  function probe(): Probe {
    return TestBed.createComponent(LanguageManagementComponent)
      .componentInstance as unknown as Probe;
  }

  describe('construction', () => {
    it('loads the list without waiting for ngOnInit', () => {
      const cmp = probe();
      expect(getAllList).toHaveBeenCalled();
      expect(cmp.rows()).toEqual([english]);
      expect(cmp.loading()).toBeFalse();
    });

    it('reads the update policy by its real name', () => {
      probe();
      expect(getGrantedPolicy).toHaveBeenCalledWith('LanguageManagement.Languages.Update');
    });

    it('withholds management when the policy is not granted', () => {
      getGrantedPolicy.and.returnValue(false);
      expect(probe().canManage()).toBeFalse();
    });

    // Positive control for the test above.
    it('allows management when the policy is granted', () => {
      expect(probe().canManage()).toBeTrue();
    });

    it('empties the list and clears loading when it cannot load', () => {
      getAllList.and.returnValue(throwError(() => new Error('boom')));
      const cmp = probe();
      expect(cmp.rows()).toEqual([]);
      expect(cmp.loading()).toBeFalse();
    });
  });

  describe('flagLabel', () => {
    it('takes the language half of a culture', () => {
      expect(probe().flagLabel('zh-Hans')).toBe('ZH');
    });

    it('upper-cases a bare culture', () => {
      expect(probe().flagLabel('en')).toBe('EN');
    });

    it('falls back to a question mark when there is no culture', () => {
      const cmp = probe();
      expect(cmp.flagLabel(null)).toBe('?');
      expect(cmp.flagLabel(undefined)).toBe('?');
      expect(cmp.flagLabel('')).toBe('?');
    });
  });

  describe('opening the modal', () => {
    it('opens a blank enabled draft for a new language', () => {
      const cmp = probe();
      cmp.openNew();
      expect(cmp.editing()).toBe('new');
      expect(cmp.draft()).toEqual({
        displayName: '',
        cultureName: '',
        uiCultureName: '',
        flagIcon: '',
        isEnabled: true,
      });
    });

    it('edits by id and carries the concurrency stamp into the draft', () => {
      const cmp = probe();
      cmp.openEdit(english);
      expect(cmp.editing()).toBe('lang-en');
      expect(cmp.draft()).toEqual({
        displayName: 'English',
        cultureName: 'en',
        uiCultureName: 'en',
        flagIcon: 'flag-en',
        isEnabled: true,
        concurrencyStamp: 'stamp-en',
      });
    });

    it('falls back to empty strings for a sparse row', () => {
      const cmp = probe();
      cmp.openEdit({ isEnabled: false });
      expect(cmp.editing()).toBeNull();
      expect(cmp.draft()).toEqual({
        displayName: '',
        cultureName: '',
        uiCultureName: '',
        flagIcon: '',
        isEnabled: false,
        concurrencyStamp: undefined,
      });
    });

    it('merges a patch into the open draft', () => {
      const cmp = probe();
      cmp.openNew();
      cmp.patchDraft({ displayName: 'Spanish' });
      expect(cmp.draft()?.['displayName']).toBe('Spanish');
      expect(cmp.draft()?.['isEnabled']).toBeTrue();
    });

    it('ignores a patch when nothing is open', () => {
      const cmp = probe();
      cmp.patchDraft({ displayName: 'Spanish' });
      expect(cmp.draft()).toBeNull();
    });

    it('closes both the mode and the draft', () => {
      const cmp = probe();
      cmp.openEdit(english);
      cmp.close();
      expect(cmp.editing()).toBeNull();
      expect(cmp.draft()).toBeNull();
    });

    it('refuses to close while a save is in flight', () => {
      const cmp = probe();
      cmp.openEdit(english);
      cmp.isBusy.set(true);
      cmp.close();
      expect(cmp.editing()).toBe('lang-en');
    });
  });

  describe('save', () => {
    it('does nothing with no draft open', () => {
      probe().save();
      expect(create).not.toHaveBeenCalled();
      expect(update).not.toHaveBeenCalled();
    });

    it('does nothing while a save is in flight', () => {
      const cmp = probe();
      cmp.openNew();
      cmp.patchDraft({ displayName: 'Spanish', cultureName: 'es' });
      cmp.isBusy.set(true);
      cmp.save();
      expect(create).not.toHaveBeenCalled();
    });

    it('warns when the display name is blank', () => {
      const cmp = probe();
      cmp.openNew();
      cmp.patchDraft({ displayName: '  ', cultureName: 'es' });
      cmp.save();
      expect(warn).toHaveBeenCalledWith('Display name and culture are required.');
      expect(create).not.toHaveBeenCalled();
    });

    it('warns when a NEW language has no culture', () => {
      const cmp = probe();
      cmp.openNew();
      cmp.patchDraft({ displayName: 'Spanish', cultureName: '   ' });
      cmp.save();
      expect(warn).toHaveBeenCalled();
      expect(create).not.toHaveBeenCalled();
    });

    // The culture is immutable after creation, so the edit path must NOT demand it. This is
    // the branch that separates `mode === 'new' && !cultureName` from a blanket check.
    it('allows an edit to save without a culture', () => {
      const cmp = probe();
      cmp.openEdit({ ...english, cultureName: '' });
      cmp.save();
      expect(warn).not.toHaveBeenCalled();
      expect(update).toHaveBeenCalled();
    });

    it('creates with trimmed values', () => {
      const cmp = probe();
      cmp.openNew();
      cmp.patchDraft({
        displayName: '  Spanish  ',
        cultureName: '  es  ',
        uiCultureName: '  es-ES  ',
        flagIcon: '  flag-es  ',
        isEnabled: true,
      });
      cmp.save();
      expect(create).toHaveBeenCalledWith({
        displayName: 'Spanish',
        cultureName: 'es',
        uiCultureName: 'es-ES',
        flagIcon: 'flag-es',
        isEnabled: true,
      });
      expect(update).not.toHaveBeenCalled();
    });

    it('falls back to the culture when no UI culture is given', () => {
      const cmp = probe();
      cmp.openNew();
      cmp.patchDraft({ displayName: 'Spanish', cultureName: 'es', uiCultureName: '  ' });
      cmp.save();
      expect(create.calls.mostRecent().args[0].uiCultureName).toBe('es');
    });

    it('sends no flag icon rather than an empty one', () => {
      const cmp = probe();
      cmp.openNew();
      cmp.patchDraft({ displayName: 'Spanish', cultureName: 'es', flagIcon: '   ' });
      cmp.save();
      expect(create.calls.mostRecent().args[0].flagIcon).toBeUndefined();
    });

    it('updates the edited id and does not resend the culture', () => {
      const cmp = probe();
      cmp.openEdit(english);
      cmp.patchDraft({ displayName: 'British English' });
      cmp.save();
      expect(update).toHaveBeenCalledWith('lang-en', {
        displayName: 'British English',
        flagIcon: 'flag-en',
        isEnabled: true,
        concurrencyStamp: 'stamp-en',
      });
      expect(create).not.toHaveBeenCalled();
    });

    it('closes and reloads on success', () => {
      const cmp = probe();
      cmp.openEdit(english);
      getAllList.calls.reset();
      cmp.save();
      expect(success).toHaveBeenCalledWith('Language saved.');
      expect(cmp.editing()).toBeNull();
      expect(cmp.draft()).toBeNull();
      expect(getAllList).toHaveBeenCalled();
      expect(cmp.isBusy()).toBeFalse();
    });

    it('keeps the draft open and releases the busy flag when the save fails', () => {
      update.and.returnValue(throwError(() => new Error('boom')));
      const cmp = probe();
      cmp.openEdit(english);
      cmp.save();
      expect(cmp.draft()).not.toBeNull();
      expect(cmp.isBusy()).toBeFalse();
    });
  });

  describe('setDefault', () => {
    it('does nothing for a row with no id', () => {
      probe().setDefault({ displayName: 'No id' });
      expect(setAsDefault).not.toHaveBeenCalled();
    });

    it('does nothing while another write is in flight', () => {
      const cmp = probe();
      cmp.isBusy.set(true);
      cmp.setDefault(english);
      expect(setAsDefault).not.toHaveBeenCalled();
    });

    it('promotes the chosen language and reloads', () => {
      const cmp = probe();
      getAllList.calls.reset();
      cmp.setDefault(english);
      expect(setAsDefault).toHaveBeenCalledWith('lang-en');
      expect(success).toHaveBeenCalledWith('Default language updated.');
      expect(getAllList).toHaveBeenCalled();
    });

    it('releases the busy flag when it fails', () => {
      setAsDefault.and.returnValue(throwError(() => new Error('boom')));
      const cmp = probe();
      cmp.setDefault(english);
      expect(cmp.isBusy()).toBeFalse();
    });
  });

  describe('remove', () => {
    it('does nothing for a row with no id', () => {
      probe().remove({ displayName: 'No id' });
      expect(remove).not.toHaveBeenCalled();
    });

    // The business rule: the default language cannot be removed.
    it('refuses to remove the default language', () => {
      probe().remove({ ...english, isDefaultLanguage: true });
      expect(remove).not.toHaveBeenCalled();
    });

    it('does nothing while another write is in flight', () => {
      const cmp = probe();
      cmp.isBusy.set(true);
      cmp.remove(english);
      expect(remove).not.toHaveBeenCalled();
    });

    // Positive control for all three refusals: a non-default row with an id and no write in
    // flight IS deleted. Without this the three tests above pass with remove() gutted.
    it('removes a non-default language and reloads', () => {
      const cmp = probe();
      getAllList.calls.reset();
      cmp.remove(english);
      expect(remove).toHaveBeenCalledWith('lang-en');
      expect(success).toHaveBeenCalledWith('Language removed.');
      expect(getAllList).toHaveBeenCalled();
    });

    it('releases the busy flag when it fails', () => {
      remove.and.returnValue(throwError(() => new Error('boom')));
      const cmp = probe();
      cmp.remove(english);
      expect(cmp.isBusy()).toBeFalse();
    });
  });
});
