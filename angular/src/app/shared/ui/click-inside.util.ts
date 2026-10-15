/**
 * Whether a click landed inside an element matching `selector`, read from the event's dispatch
 * path.
 *
 * Used by the document click listeners that close a menu when a click lands outside it. The path
 * is fixed when the event is dispatched; `target.closest()` is not. Under zone.js, change
 * detection runs between the clicked control's own handler and the document listener, so a
 * control that hides itself when clicked (the notifications menu's "Mark all read") is already
 * detached by then, `closest()` finds no wrapper, and the menu would close on a click that landed
 * inside it.
 */
export function clickLandedInside(event: Event, selector: string): boolean {
  return event.composedPath().some((node) => node instanceof Element && node.matches(selector));
}
