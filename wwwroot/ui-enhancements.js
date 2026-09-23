function statusClass(s){return 'status-'+String(s||'New').toLowerCase().replace(/[^a-z]+/g,'-')}
function enhanceBookingTable(){
 document.querySelectorAll('#rows tr').forEach(tr=>{
   if(tr.cells.length<9)return;
   const status=tr.cells[7].textContent.trim();
   tr.cells[7].innerHTML=`<span class="statusPill ${statusClass(status)}">${safe(status)}</span>`;
   const papers=tr.cells[8].textContent.trim();
   tr.cells[8].innerHTML= papers.includes('Received')?'<span class="paperPill received">✓ Received</span>':'<span class="paperPill pending">● Pending</span>';
   if(status==='Cancelled')tr.classList.add('cancelledRow');
 });
}
function dashboardInsights(){
 const today=new Date().toISOString().slice(0,10);
 const tomorrow=new Date(Date.now()+86400000).toISOString().slice(0,10);
 const active=bookings.filter(x=>x.status!=='Cancelled');
 const t=active.filter(x=>x.bookingDate.slice(0,10)===today);
 const tm=active.filter(x=>x.bookingDate.slice(0,10)===tomorrow);
 const pending=t.filter(x=>!x.papersReceived).length;
 let bar=document.getElementById('smartInsights');
 if(!bar){bar=document.createElement('div');bar.id='smartInsights';bar.className='smartInsights';document.querySelector('.stats')?.after(bar)}
 bar.innerHTML=`<div><b>${tm.length}</b><span>Tomorrow</span></div><div><b>${pending}</b><span>Today's papers pending</span></div><div><b>${active.filter(x=>x.status==='Completed'&&x.bookingDate.slice(0,10)===today).length}</b><span>Completed today</span></div><div><b>${active.filter(x=>x.bookingDate.slice(0,10)>=today).length}</b><span>Upcoming bookings</span></div>`;
}
function detectDoctorLoad(){
 const d=el('bookingDate')?.value,doctor=el('doctor')?.value,shift=el('shift')?.value;
 if(!d||!doctor)return;
 const same=bookings.filter(x=>x.status!=='Cancelled'&&x.bookingDate.slice(0,10)===d&&x.doctor===doctor&&x.shift===shift&&x.id!==el('editId')?.value);
 const box=el('capacityInfo'); if(box&&same.length)box.title=`${same.length} existing ${shift.toLowerCase()} booking(s) for ${doctor} on this date`;
}
function installUiEnhancements(){
 const oldRender=window.render;
 window.render=function(){oldRender();enhanceBookingTable();dashboardInsights()};
 ['doctor','bookingDate','shift'].forEach(id=>el(id)?.addEventListener('change',detectDoctorLoad));
 if(typeof render==='function')render();
}
window.addEventListener('load',installUiEnhancements);
