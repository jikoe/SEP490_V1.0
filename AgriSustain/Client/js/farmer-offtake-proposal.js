import { request } from './page-ui.js';
import { byId, detailMarkup, notice } from './offtake-ui.js';

const id = new URLSearchParams(location.search).get('id');
const message = byId('farmer-proposal-notice');
const panel = byId('farmer-proposal-detail');

function signIn() {
  location.replace('../Guest/login.html?offtake=' + id);
}

async function loadProposal() {
  notice(message, 'Đang tải đề xuất…');
  try {
    const detail = await request('/farmer/offtake-proposals/' + id);
    panel.innerHTML = detailMarkup(detail);
    panel.hidden = false;
    byId('farmer-proposal-contract').href = 'offtake-contracts.html?id=' + id;
    byId('farmer-proposal-actions').hidden = false;
    notice(message, '');
  } catch (error) {
    if (error.status === 401) signIn();
    else notice(message, error.message, true);
  }
}

if (!/^[1-9]\d*$/.test(id ?? '')) {
  notice(message, 'Liên kết đề xuất không hợp lệ.', true);
} else if (!Auth.isAuthenticated()) {
  signIn();
} else if (Auth.requireAuth('Farmer')) {
  loadProposal();
}
