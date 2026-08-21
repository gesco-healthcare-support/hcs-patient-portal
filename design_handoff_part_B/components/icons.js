/* Simple line-icon set for app chrome. window.Icon('name', size?) */
(function () {
  const P = {
    search:   '<circle cx="11" cy="11" r="7"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>',
    calendar: '<rect x="3" y="4.5" width="18" height="16" rx="2.5"/><line x1="3" y1="9" x2="21" y2="9"/><line x1="8" y1="2.5" x2="8" y2="6.5"/><line x1="16" y1="2.5" x2="16" y2="6.5"/>',
    clock:    '<circle cx="12" cy="12" r="8.5"/><path d="M12 7.5v5l3 2"/>',
    user:     '<circle cx="12" cy="8" r="4"/><path d="M4 20.5c0-4 3.6-6.5 8-6.5s8 2.5 8 6.5"/>',
    users:    '<circle cx="9" cy="8" r="3.4"/><path d="M2.5 20c0-3.4 2.9-5.5 6.5-5.5s6.5 2.1 6.5 5.5"/><path d="M16 5.2a3.4 3.4 0 0 1 0 6.6"/><path d="M17.5 14.7c2.6.5 4 2.4 4 5.3"/>',
    plus:     '<line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>',
    chevDown: '<polyline points="6 9 12 15 18 9"/>',
    chevRight:'<polyline points="9 6 15 12 9 18"/>',
    chevLeft: '<polyline points="15 6 9 12 15 18"/>',
    refresh:  '<path d="M21 12a9 9 0 1 1-2.64-6.36"/><polyline points="21 3 21 9 15 9"/>',
    x:        '<line x1="6" y1="6" x2="18" y2="18"/><line x1="18" y1="6" x2="6" y2="18"/>',
    filter:   '<path d="M3 5h18l-7 8v6l-4 2v-8z"/>',
    bell:     '<path d="M18 8a6 6 0 1 0-12 0c0 7-3 8-3 8h18s-3-1-3-8"/><path d="M13.7 21a2 2 0 0 1-3.4 0"/>',
    grid:     '<rect x="3" y="3" width="7.5" height="7.5" rx="1.5"/><rect x="13.5" y="3" width="7.5" height="7.5" rx="1.5"/><rect x="3" y="13.5" width="7.5" height="7.5" rx="1.5"/><rect x="13.5" y="13.5" width="7.5" height="7.5" rx="1.5"/>',
    list:     '<line x1="8" y1="6" x2="21" y2="6"/><line x1="8" y1="12" x2="21" y2="12"/><line x1="8" y1="18" x2="21" y2="18"/><circle cx="3.5" cy="6" r="1.3"/><circle cx="3.5" cy="12" r="1.3"/><circle cx="3.5" cy="18" r="1.3"/>',
    doc:      '<path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><polyline points="14 3 14 8 19 8"/>',
    map:      '<path d="M12 21s-6.5-5.5-6.5-10.5A6.5 6.5 0 0 1 18.5 10.5C18.5 15.5 12 21 12 21z"/><circle cx="12" cy="10.5" r="2.4"/>',
    stetho:   '<path d="M5 3v5a4 4 0 0 0 8 0V3"/><path d="M9 16v1a4 4 0 0 0 8 0v-2"/><circle cx="18" cy="11" r="2.2"/>',
    settings: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.6 1.6 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.6 1.6 0 0 0-2.7 1.1V21a2 2 0 0 1-4 0v-.1A1.6 1.6 0 0 0 6.8 19.3a2 2 0 1 1-2.8-2.8l.1-.1a1.6 1.6 0 0 0-1.1-2.7H3a2 2 0 0 1 0-4h.1A1.6 1.6 0 0 0 4.7 6.8a2 2 0 1 1 2.8-2.8l.1.1a1.6 1.6 0 0 0 2.7-1.1V3a2 2 0 0 1 4 0v.1a1.6 1.6 0 0 0 2.7 1.1l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.6 1.6 0 0 0-.3 1.8z"/>',
    logout:   '<path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/>',
    alert:    '<path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/>',
    check:    '<polyline points="20 6 9 17 4 12"/>',
    arrowUp:  '<line x1="12" y1="19" x2="12" y2="5"/><polyline points="6 11 12 5 18 11"/>',
    arrowDown:'<line x1="12" y1="5" x2="12" y2="19"/><polyline points="6 13 12 19 18 13"/>',
    dots:     '<circle cx="5" cy="12" r="1.6"/><circle cx="12" cy="12" r="1.6"/><circle cx="19" cy="12" r="1.6"/>',
    sort:     '<path d="M8 9l4-4 4 4"/><path d="M8 15l4 4 4-4"/>',
    money:    '<rect x="2.5" y="6" width="19" height="12" rx="2"/><circle cx="12" cy="12" r="2.6"/><line x1="6" y1="9.5" x2="6" y2="9.51"/><line x1="18" y1="14.5" x2="18" y2="14.51"/>',
    inbox:    '<path d="M22 12h-6l-2 3h-4l-2-3H2"/><path d="M5.5 5.5h13L22 12v6a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2v-6z"/>',
    home:     '<path d="M3 11.5 12 4l9 7.5"/><path d="M5 10v10h14V10"/>',
    eye:      '<path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7z"/><circle cx="12" cy="12" r="3"/>',
    help:     '<circle cx="12" cy="12" r="9"/><path d="M9.4 9.2a2.7 2.7 0 0 1 5.1 1.1c0 1.8-2.5 2-2.5 3.6"/><line x1="12" y1="17.2" x2="12.01" y2="17.2"/>',
    lifebuoy: '<circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="3.4"/><line x1="5.6" y1="5.6" x2="9.6" y2="9.6"/><line x1="14.4" y1="14.4" x2="18.4" y2="18.4"/><line x1="18.4" y1="5.6" x2="14.4" y2="9.6"/><line x1="9.6" y1="14.4" x2="5.6" y2="18.4"/>',
    folder:   '<path d="M3 7a2 2 0 0 1 2-2h3.5l2 2.5H19a2 2 0 0 1 2 2V18a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>',
    folderOpen:'<path d="M3 7a2 2 0 0 1 2-2h3.5l2 2.5H19a2 2 0 0 1 2 2"/><path d="M3 9.5h17.5a1 1 0 0 1 .97 1.24l-1.5 6A1.5 1.5 0 0 1 18.5 18H4.5A1.5 1.5 0 0 1 3 16.5z"/>',
    key:      '<circle cx="7.5" cy="15.5" r="4"/><line x1="10.3" y1="12.7" x2="20" y2="3"/><line x1="16.5" y1="6.5" x2="19.5" y2="9.5"/><line x1="13.7" y1="9.3" x2="16.7" y2="12.3"/>',
    download: '<path d="M12 3v12"/><polyline points="7 10 12 15 17 10"/><path d="M5 21h14"/>',
    upload:   '<path d="M12 21V9"/><polyline points="7 14 12 9 17 14"/><path d="M5 5h14"/>',
    globe:    '<circle cx="12" cy="12" r="9"/><line x1="3" y1="12" x2="21" y2="12"/><path d="M12 3c2.6 2.4 4 5.6 4 9s-1.4 6.6-4 9c-2.6-2.4-4-5.6-4-9s1.4-6.6 4-9z"/>',
    trash:    '<polyline points="3 6 21 6"/><path d="M8 6V4h8v2"/><path d="M6 6l1 14h10l1-14"/>',
    edit:     '<path d="M4 20h4l9.5-9.5-4-4L4 16z"/><line x1="13.5" y1="6.5" x2="17.5" y2="10.5"/>',
    file:     '<path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><polyline points="14 3 14 8 19 8"/>',
  };
  window.Icon = function (name, size) {
    const s = size || 18;
    return '<svg viewBox="0 0 24 24" width="' + s + '" height="' + s + '" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + (P[name] || '') + '</svg>';
  };
  // React-friendly: returns an object usable with dangerouslySetInnerHTML
  window.Ico = function (name, size) {
    return { __html: window.Icon(name, size) };
  };
})();
