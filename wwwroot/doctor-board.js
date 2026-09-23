function showDoctorBoard(){
 const date=el('day')?.value||new Date().toISOString().slice(0,10);
 const active=bookings.filter(b=>b.status!=='Cancelled'&&b.bookingDate.slice(0,10)===date);
 const docs=[...new Set([...doctors,...active.map(b=>b.doctor)].filter(Boolean))];
 const cap=(doc,shift)=>{const r=capacities.find(x=>x.doctor===doc&&x.shift===shift);return r?r.maxBookings:null};
 const cell=(doc,shift)=>{const used=active.filter(b=>b.doctor===doc&&b.shift===shift).length,max=cap(doc,shift),left=max==null?'—':Math.max(0,max-used),pct=max?Math.min(100,Math.round(used/max*100)):0;return `<div class="shiftLoad"><b>${used}${max!=null?' / '+max:''}</b><span>${max==null?'No capacity set':left+' remaining'}</span>${max!=null?`<i><em style="width:${pct}%"></em></i>`:''}</div>`};
 el('adminPanel').classList.remove('hidden');
 el('adminPanel').innerHTML=`<div class="boardHead"><div><h2>Doctor Daily Board</h2><p>Live booking load by doctor and shift</p></div><input id="boardDate" type="date" value="${date}" onchange="el('day').value=this.value;showDoctorBoard()"></div><div class="table"><table><thead><tr><th>Doctor</th><th>Morning</th><th>Evening</th><th>Total</th></tr></thead><tbody>${docs.map(d=>`<tr><td><b>${safe(d)}</b></td><td>${cell(d,'Morning')}</td><td>${cell(d,'Evening')}</td><td><b>${active.filter(b=>b.doctor===d).length}</b></td></tr>`).join('')}</tbody></table></div>`;
}
function installDoctorBoard(){
 const nav=[...document.querySelectorAll('aside nav a')];
 const doctorsLink=nav.find(a=>a.textContent.trim()==='Doctors');
 if(doctorsLink){const board=document.createElement('a');board.textContent='Doctor Board';board.onclick=showDoctorBoard;doctorsLink.after(board)}
}
window.addEventListener('load',installDoctorBoard);
