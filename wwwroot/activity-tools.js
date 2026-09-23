// Enhanced Activity Log UI loaded after app.js.
window.showActivity = async function () {
  const all = await (await fetch('/api/activity')).json();
  const panel = el('adminPanel');
  panel.classList.remove('hidden');
  panel.innerHTML = `
    <h2>Activity Log</h2>
    <div class="tools activityFilters">
      <input id="activitySearch" placeholder="Search user, action or details">
      <select id="activityAction"><option value="">All actions</option></select>
      <input id="activityDate" type="date">
      <button id="activityClear">Clear</button>
    </div>
    <div class="activitySummary" id="activitySummary"></div>
    <div class="table"><table>
      <thead><tr><th>Date / Time</th><th>User</th><th>Action</th><th>Details</th></tr></thead>
      <tbody id="activityRows"></tbody>
    </table></div>`;

  const actions = [...new Set(all.map(x => x.action).filter(Boolean))].sort();
  const actionSelect = el('activityAction');
  actionSelect.innerHTML += actions.map(x => `<option>${safe(x)}</option>`).join('');

  function draw() {
    const q = el('activitySearch').value.trim().toLowerCase();
    const action = actionSelect.value;
    const date = el('activityDate').value;
    const rows = all.filter(x => {
      const hay = [x.user, x.action, x.detail].join(' ').toLowerCase();
      const day = x.time ? new Date(x.time).toISOString().slice(0, 10) : '';
      return (!q || hay.includes(q)) && (!action || x.action === action) && (!date || day === date);
    });
    el('activitySummary').textContent = `${rows.length} of ${all.length} activities`;
    el('activityRows').innerHTML = rows.map(x => `<tr>
      <td>${new Date(x.time).toLocaleString()}</td>
      <td>${safe(x.user)}</td>
      <td><span class="badge">${safe(x.action)}</span></td>
      <td>${safe(x.detail)}</td>
    </tr>`).join('');
  }

  el('activitySearch').addEventListener('input', draw);
  actionSelect.addEventListener('change', draw);
  el('activityDate').addEventListener('change', draw);
  el('activityClear').addEventListener('click', () => {
    el('activitySearch').value = '';
    actionSelect.value = '';
    el('activityDate').value = '';
    draw();
  });
  draw();
};