/* ==========================================================================
   AgriSustain - Private dossier files (js/dossier.js)
   Legal documents (Business License, certificate dossiers) are served from
   /api/dossiers/... and need the bearer token, so they are fetched as a blob.
   ========================================================================== */

const Dossier = {
  async open(url) {
    const path = String(url || '').replace(/^\/api/, '');
    const token = Auth.getAccessToken();
    const response = await fetch(`${CONFIG.API_BASE_URL}${path}`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {}
    });
    if (!response.ok) {
      Utils.showToast('Không mở được tài liệu.', 'error');
      return;
    }
    const blob = await response.blob();
    window.open(URL.createObjectURL(blob), '_blank', 'noopener');
  },

  /** Returns an error message when the chosen file is not allowed, otherwise null. */
  check(file, extensions) {
    if (!file) return null;
    const ext = file.name.toLowerCase().split('.').pop();
    if (!extensions.includes(ext)) return `Chỉ hỗ trợ tệp ${extensions.map((e) => e.toUpperCase()).join(', ')}.`;
    if (file.size > 5 * 1024 * 1024) return 'Tệp vượt quá 5 MB. Vui lòng chọn tệp nhỏ hơn.';
    return null;
  }
};
