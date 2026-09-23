function bookingActionButtons(b){
  const edit=`<button onclick='editBooking(${JSON.stringify(b)})'>Edit</button>`;
  const history=`<button onclick='patientHistory(${JSON.stringify(b.phone||"")},${JSON.stringify(b.patientName)})'>History</button>`;
  const cancel=b.status==='Cancelled'?'':` <button class="danger" onclick="cancelBooking('${b.id}')">Cancel</button>`;
  return edit+' '+history+cancel;
}
async function cancelBooking(id){
  if(!allowed('bookings.delete')) return alert('Permission denied: cancellation requires bookings.delete permission.');
  const b=bookings.find(x=>x.id===id); if(!b)return;
  const reason=prompt('Cancellation reason for '+b.patientName+':');
  if(reason===null)return;
  if(!reason.trim())return alert('Cancellation reason is required.');
  if(!confirm('Cancel booking '+id+' for '+b.patientName+'?'))return;
  const note='Cancelled by '+me.name+' — Reason: '+reason.trim();
  const updated={...b,status:'Cancelled',notes:(b.notes?b.notes+' | ':'')+note,modifiedBy:me.name};
  const r=await fetch('/api/bookings/'+encodeURIComponent(id),{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify(updated)});
  if(!r.ok)return alert('Could not cancel booking.');
  await load();
  alert('Booking cancelled successfully.');
}
function installBookingActions(){
  if(window.__bookingActionsInstalled)return;
  window.__bookingActionsInstalled=true;
  const oldRender=window.render;
  window.render=function(){
    oldRender();
    const trs=[...document.querySelectorAll('#rows tr')];
    trs.forEach(tr=>{
      const id=tr.cells?.[0]?.textContent?.trim();
      const b=bookings.find(x=>x.id===id);
      if(b&&tr.cells?.length)tr.cells[tr.cells.length-1].innerHTML=bookingActionButtons(b);
    });
  };
  if(typeof render==='function')render();
}
window.addEventListener('load',installBookingActions);
