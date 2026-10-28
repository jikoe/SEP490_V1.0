// Hide report reference markers in user-facing text for the product/cart screens.
export function displayMessage(value) {
  return String(value ?? '').replace(/\s*\(MSG\d+\)/g, '')
    .replace(/([.!?])\.+(?=\s|$)/g, '$1');
}
