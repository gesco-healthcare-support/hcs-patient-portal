import { clickLandedInside } from './click-inside.util';

/**
 * The helper is only meaningful while an event is being dispatched (the path is emptied
 * afterwards), so each test reads it from a document listener during a real click, which is where
 * the menus call it.
 */
describe('clickLandedInside', () => {
  const planted: HTMLElement[] = [];
  let seen: boolean | undefined;
  const probe = (event: Event): void => {
    seen = clickLandedInside(event, '.menu-wrap');
  };

  /** A `.menu-wrap` holding one button, plus a button outside it, both attached to the document. */
  function plant(): { inside: HTMLButtonElement; outside: HTMLButtonElement } {
    const wrapper = document.createElement('div');
    wrapper.className = 'menu-wrap';
    const inside = document.createElement('button');
    wrapper.appendChild(inside);
    const outside = document.createElement('button');
    document.body.append(wrapper, outside);
    planted.push(wrapper, outside);
    return { inside, outside };
  }

  beforeEach(() => {
    seen = undefined;
    document.addEventListener('click', probe);
  });

  afterEach(() => {
    document.removeEventListener('click', probe);
    planted.splice(0).forEach((el) => el.remove());
  });

  it('is true for a click on a control inside the wrapper', () => {
    plant().inside.click();

    expect(seen).toBeTrue();
  });

  it('is false for a click outside the wrapper', () => {
    plant().outside.click();

    expect(seen).toBeFalse();
  });

  it('stays true when the clicked control detached itself before the document listener', () => {
    const { inside } = plant();
    let closestAtDocument: Element | null | undefined;
    // Load-bearing: this stands in for change detection re-rendering the control away between its
    // own handler and the document listener. Without it the test cannot tell the path from
    // closest().
    inside.addEventListener('click', () => inside.remove());
    const recordClosest = (): void => {
      closestAtDocument = inside.closest('.menu-wrap');
    };
    document.addEventListener('click', recordClosest);

    inside.click();
    document.removeEventListener('click', recordClosest);

    expect(closestAtDocument).withContext('the fixture must detach the control').toBeNull();
    expect(seen).toBeTrue();
  });
});
