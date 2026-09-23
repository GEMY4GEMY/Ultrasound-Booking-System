let activityRows=[];
async function showActivityAdvanced(){
  activityRows=await(await fetch('/api/activity')).json();
  const users=[...new Set(activityRows.map(x=>x.user).filter(Boolean))].sort();
  const actions=[...new Set(activityRows.map(x=>x.action).filter(Boolean))].sort();
  el('adminPanel').classList.remove('hidden');
  el('adminPanel').innerHTML=`<h2>Activity Log</h2><div class="tools"><input id="alogSearch" oninput="renderActivityAdvanced()" placeholder="Search user, action or details"><select id="alogUser" onchange="renderActivityAdvanced()"><option value="">All users</option>${users.map(x=>`<option>${safe(x)}</option>`).join('')}</select><select id="alogAction" onchange="renderActivityAdvanced()"><option value="">All actions</option>${actions.map(x=>`<option>${safe(x)}</option>`).join('')}</select><input id="alogDate" type="date" onchange="renderActivityAdvanced()"><button onclick="clearActivityFilters()">Clear</button></div><div id="alogCount"></div><div class="table"><table><thead><tr><th>Date / Time</th><th>User</th><th>Action</th><th>Details</th></tr></thead><tbody id="alogRows"></tbody></table></div>`;
  renderActivityAdvanced();
}
function renderActivityAdvanced(){
  const q=(el('alogSearch')?.value||'').toLowerCase(),u=el('alogUser')?.value||'',a=el('alogAction')?.value||'',d=el('alogDate')?.value||'';
  const rows=activityRows.filter(x=>(!q||[x.user,x.action,x.detail].some(v=>(v||'').toLowerCase().includes(q)))&&(!u||x.user===u)&&(!a||x.action===a)&&(!d||String(x.time).slice(0,10)===d));
  el('alogCount').innerHTML=`<p><b>${rows.length}</b> matching activities</p>`;
  el('alogRows').innerHTML=rows.map(x=>`<tr><td>${new Date(x.time).toLocaleString()}</td><td>${safe(x.user)}</td><td><span class="badge">${safe(x.action)}</span></td><td>${safe(x.detail)}</td></tr>`).join('');
}
function clearActivityFilters(){['alogSearch','alogUser','alogAction','alogDate'].forEach(id=>{if(el(id))el(id).value=''});renderActivityAdvanced();}
