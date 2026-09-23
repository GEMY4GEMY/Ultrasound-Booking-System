async function showNetworkTools(){
 let health={};try{health=await(await fetch('/api/health')).json()}catch(e){}
 const host=location.hostname,port=location.port||'5090',clientUrl=`http://${host}:${port}`;
 el('adminPanel').classList.remove('hidden');
 el('adminPanel').innerHTML=`<div class="boardHead"><div><h2>Network & Server</h2><p>Use the main computer as the Ultrasound Booking server.</p></div><span class="online">● ${health.status==='ok'?'SERVER ONLINE':'CHECK SERVER'}</span></div><div class="networkCards"><div><small>Server computer</small><b>${safe(health.server||host)}</b></div><div><small>Port</small><b>${safe(port)}</b></div><div><small>Bookings</small><b>${health.bookings??'—'}</b></div><div><small>Users</small><b>${health.users??'—'}</b></div></div><div class="networkHelp"><h3>Access from another device</h3><p>Connect the other computer to the same local network, then open this address in its browser:</p><div class="networkUrl"><code>${safe(clientUrl)}</code><button onclick="copyNetworkUrl('${clientUrl}')">Copy</button></div><p class="muted">For other devices, the server computer should stay powered on and Windows Firewall must allow the application on Private networks.</p><h3>Data location</h3><code>${safe(health.dataPath||'Application data folder')}</code></div>`;
}
async function copyNetworkUrl(url){try{await navigator.clipboard.writeText(url);alert('Network address copied.')}catch(e){prompt('Copy network address:',url)}}
function installNetworkTools(){const nav=document.querySelector('aside nav');if(!nav)return;const a=document.createElement('a');a.textContent='Network & Server';a.onclick=showNetworkTools;nav.appendChild(a)}
window.addEventListener('load',installNetworkTools);
