let bookingGuardTimer=null;
function normalizePhone(v){return String(v||'').replace(/\D/g,'')}
function bookingGuardCheck(){
 const name=(el('patient')?.value||'').trim().toLowerCase(), phone=normalizePhone(el('phone')?.value), date=el('bookingDate')?.value, doctor=el('doctor')?.value, shift=el('shift')?.value, edit=el('editId')?.value;
 let box=document.getElementById('bookingGuard');
 if(!box){box=document.createElement('div');box.id='bookingGuard';box.className='bookingGuard';el('capacityInfo')?.after(box)}
 if((!name&&!phone)||!date){box.innerHTML='';box.className='bookingGuard';return}
 const matches=bookings.filter(b=>b.id!==edit&&b.status!=='Cancelled'&&b.bookingDate.slice(0,10)===date&&((phone&&normalizePhone(b.phone)===phone)||(name&&String(b.patientName||'').trim().toLowerCase()===name)));
 if(!matches.length){box.innerHTML='<span>✓ No duplicate patient booking found for this date.</span>';box.className='bookingGuard ok';return}
 const exact=matches.some(b=>b.doctor===doctor&&b.shift===shift);
 box.className='bookingGuard '+(exact?'danger':'warn');
 box.innerHTML=`<b>${exact?'Possible duplicate booking':'Patient already booked on this date'}</b><span>${matches.map(b=>`${safe(b.patientName)} • ${safe(b.exam)} • ${safe(b.doctor)} • ${safe(b.shift)}`).join('<br>')}</span>`;
}
function installBookingGuard(){
 ['patient','phone','bookingDate','doctor','shift'].forEach(id=>el(id)?.addEventListener('input',()=>{clearTimeout(bookingGuardTimer);bookingGuardTimer=setTimeout(bookingGuardCheck,120)}));
 ['bookingDate','doctor','shift'].forEach(id=>el(id)?.addEventListener('change',bookingGuardCheck));
 const oldQuick=window.quickBooking; if(oldQuick)window.quickBooking=function(){oldQuick();setTimeout(bookingGuardCheck,0)};
}
window.addEventListener('load',installBookingGuard);
