/* DareCare Admin: same-origin role-gated portal. Tokens are memory-only; no persistent storage. */
(() => {
  'use strict';

  const $ = id => document.getElementById(id);
  const state = {accessToken:null,refreshToken:null,challengeToken:null,user:null,view:'overview',usersPage:1,auditPage:1,generation:0};
  const titles = {
    overview:['نظرة عامة','المتابعة المباشرة لحالة المنصة'],
    nurses:['اعتماد التمريض','مراجعة بيانات الممرضين وطلباتهم'],
    pharmacies:['الصيدليات','مراجعة دعوات واعتمادات الصيدليات'],
    users:['المستخدمون','إدارة الحسابات وصلاحية التفعيل'],
    audit:['سجل الإجراءات','آخر العمليات المسجلة في النظام']
  };
  const statusLabels = {
    Pending:'بانتظار الرد',Accepted:'مقبول',InProgress:'جارٍ التنفيذ',
    Completed:'مكتمل',Cancelled:'ملغي',Rejected:'مرفوض',
    Quoted:'تم إرسال السعر',Confirmed:'مؤكد',Preparing:'قيد التجهيز',
    OutForDelivery:'في التوصيل',Delivered:'تم التسليم',Declined:'مرفوض'
  };

  const element = (tag, cls='', text='') => {
    const n=document.createElement(tag);
    if(cls)n.className=cls;
    n.textContent=String(text??'');
    return n;
  };
  const cleanNumber = n => Number.isFinite(Number(n)) ? Number(n).toLocaleString('ar-EG') : '—';
  function prettyDate(d) {
    if(!d)return 'غير محدد';
    const n=new Date(d);
    return Number.isFinite(n.getTime()) ? n.toLocaleString('ar-EG',{dateStyle:'medium',timeStyle:'short'}) : 'غير محدد';
  }
  function roleText(roles) {
    const names={Admin:'إدارة',Patient:'مريض',Nurse:'ممرض',Pharmacy:'صيدلية'};
    return (roles||[]).map(r=>names[r]||r).join('، ')||'غير محدد';
  }
  function empty(target,message) {
    target.replaceChildren(element('p','empty',message));
  }
  function notify(message,isError=false) {
    const box=$('adminToast');
    box.textContent=message;
    box.classList.toggle('error',isError);
    box.hidden=false;
    clearTimeout(notify.timer);
    notify.timer=setTimeout(()=>{box.hidden=true;},5500);
  }
  function clearError() { $('globalError').hidden=true; $('globalError').textContent=''; }
  function showError(text) {$('globalError').textContent=text;$('globalError').hidden=false;}
  function getError(obj,status) {
    if(obj && obj.errors) {
      const all=Array.isArray(obj.errors)?obj.errors:Object.values(obj.errors).flat();
      if(all.length)return all.map(String).join('، ');
    }
    return obj?.detail||obj?.message||obj?.title||('تعذر تنفيذ الطلب (HTTP '+status+')');
  }
  async function request(path,options={}) {
    const h={Accept:'application/json'};
    if(state.accessToken && !options.anonymous)h.Authorization='Bearer '+state.accessToken;
    if(options.data!==undefined)h['Content-Type']='application/json';
    let res;
    try {
      res=await fetch(path,{method:options.method||'GET',headers:h,cache:'no-store',
        credentials:'omit',body:options.data===undefined?undefined:JSON.stringify(options.data)});
    }catch(_){throw new Error('تعذر الوصول للسيرفر. تحقق من الاتصال وحاول مرة أخرى.');}
    const type=res.headers.get('content-type')||'';
    let body=null;
    if(type.includes('json')){
      try{body=await res.json();}catch(_){body=null;}
    }
    if(!res.ok){
      if(res.status===401 && state.accessToken){
        lockPortal();
        throw new Error('الجلسة انتهت. سجّل الدخول مجددًا.');
      }
      if(res.status===403)throw new Error('حسابك غير مخول لتنفيذ العملية المطلوبة.');
      throw new Error(getError(body,res.status));
    }
    return body;
  }
  function setLoginFactor(challengeToken=null) {
    state.challengeToken=challengeToken;
    $('adminCredentials').hidden=!!challengeToken;
    $('adminEmail').required=!challengeToken;
    $('adminPassword').required=!challengeToken;
    $('adminFactor').hidden=!challengeToken;
    $('adminFactorCode').required=!!challengeToken;
    $('adminFactorCode').value='';
    $('adminPassword').value='';
    $('loginSubmit').textContent=challengeToken?'تأكيد الكود':'دخول لوحة الإدارة ←';
    if(challengeToken)$('adminFactorCode').focus();
  }
  function lockPortal(message='') {
    state.accessToken=null;state.refreshToken=null;state.user=null;state.generation++;
    $('adminWorkspace').hidden=true;$('loginScreen').hidden=false;
    $('adminPassword').value='';
    $('adminLoginForm').reset();
    setLoginFactor();
    if(message){$('loginError').textContent=message;$('loginError').hidden=false;}
  }
  async function revoke(access,refresh) {
    if(!access||!refresh)return;
    try{
      await fetch('/api/v1/auth/logout',{
        method:'POST',cache:'no-store',credentials:'omit',
        headers:{Accept:'application/json','Content-Type':'application/json',Authorization:'Bearer '+access},
        body:JSON.stringify({refreshToken:refresh})
      });
    }catch(_){/* Clear the client session even if the server is unreachable. */}
  }
  async function login(e) {
    e.preventDefault();
    const btn=$('loginSubmit'),error=$('loginError');
    error.hidden=true;error.textContent='';
    btn.disabled=true;btn.textContent='جارِ التحقق من الحساب...';
    let response;
    try {
      if(state.challengeToken){
        response=await request('/api/v1/auth/authenticator/login',{anonymous:true,method:'POST',data:{
          challengeToken:state.challengeToken,code:$('adminFactorCode').value.trim()
        }});
      } else {
        response=await request('/api/v1/auth/login',{anonymous:true,method:'POST',data:{
          email:$('adminEmail').value.trim(),password:$('adminPassword').value
        }});
        if(response?.twoFactorRequired){
          setLoginFactor(response.challengeToken);
          return;
        }
      }
      const roles=Array.isArray(response?.roles)?response.roles:[];
      if(!roles.includes('Admin')){
        await revoke(response?.accessToken,response?.refreshToken);
        throw new Error('هذا الحساب لا يملك صلاحية مدير النظام.');
      }
      if(!response?.accessToken||!response?.refreshToken){
        throw new Error('استجابة الدخول غير مكتملة.');
      }
      state.accessToken=response.accessToken;
      state.refreshToken=response.refreshToken;
      const me=await request('/api/v1/auth/me');
      if(!Array.isArray(me?.roles)||!me.roles.includes('Admin')){
        const oldAccess=state.accessToken,oldRefresh=state.refreshToken;
        lockPortal();
        await revoke(oldAccess,oldRefresh);
        throw new Error('لم يتم التحقق من صلاحية الإدارة لهذا الحساب.');
      }
      // A real Admin-only API read proves the server enforces this role.
      const overview=await request('/api/v1/admin/overview');
      state.user=me;
      $('adminIdentity').textContent=me.fullName||me.email||'مدير النظام';
      $('adminPassword').value='';
      setLoginFactor();
      $('loginScreen').hidden=true;
      $('adminWorkspace').hidden=false;
      clearError();
      state.view='overview';
      setView('overview',overview);
      notify('أهلًا بك في لوحة إدارة DareCare.');
    }catch(err){
      if(!state.challengeToken||response?.accessToken)lockPortal();
      error.textContent=err.message||'تعذر تسجيل الدخول.';
      error.hidden=false;
    }finally{
      btn.disabled=false;
      btn.textContent=state.challengeToken?'تأكيد الكود':'دخول لوحة الإدارة ←';
    }
  }
  async function logout() {
    const a=state.accessToken,r=state.refreshToken;
    lockPortal();
    await revoke(a,r);
    notify('تم تسجيل الخروج من الإدارة.');
    $('adminEmail').focus();
  }
  function actionButton(label,run,className='action-btn') {
    const b=element('button',className,label);
    b.type='button';
    b.addEventListener('click',async()=>{
      if(b.disabled)return;
      b.disabled=true;
      try{await run();}catch(e){showError(e.message||'تعذر تنفيذ الإجراء.');notify(e.message,true);}
      finally{b.disabled=false;}
    });
    return b;
  }
  function dataRow(title,subtitle,actions=[]) {
    const row=element('div','data-row'),main=element('div','data-main');
    main.append(element('div','data-title',title),element('p','data-subtitle',subtitle));
    row.append(main);
    if(actions.length) {
      const div=element('div','row-actions');
      for(const a of actions)div.append(a);
      row.append(div);
    }
    return row;
  }
  function metric(label,value,icon,foot='مباشرة من النظام') {
    const box=element('article','metric');
    const top=element('div','metric-top');
    top.append(element('span','metric-label',label),element('span','metric-icon',icon));
    box.append(top,element('strong','metric-value',cleanNumber(value)),element('span','metric-foot',foot));
    return box;
  }
  function renderStatus(target,overview) {
    target.replaceChildren();
    const groups=[['الحجوزات',overview.bookings],['طلبات الدواء',overview.orders]];
    for(const [group,items] of groups) {
      if(!Array.isArray(items)||!items.length) {
        target.append(dataRow(group,'لا توجد حالات مسجلة حتى الآن.'));
        continue;
      }
      for(const item of items) {
        const b=element('div','status-card');
        b.append(element('span','',group+' · '+(statusLabels[item.status]||item.status)),
          element('strong','',cleanNumber(item.count)));
        target.append(b);
      }
    }
  }
  function preview(target,list,label) {
    target.replaceChildren();
    if(!list.length)return empty(target,'لا توجد طلبات معلّقة حاليًا.');
    for(const item of list.slice(0,4)){
      const name=item.fullName||item.businessName||label;
      const desc=[item.city||'',item.email||''].filter(Boolean).join(' · ');
      target.append(dataRow(name,desc||'في انتظار المراجعة'));
    }
  }
  function setCounts(nurses,pharmacies) {
    for(const [id,count] of [['nurseNavCount',nurses],['pharmacyNavCount',pharmacies]]) {
      const elem=$(id);
      elem.textContent=String(count);
      elem.hidden=!count;
    }
  }
  async function loadOverview(prefetched) {
    const generation=state.generation;
    const [overview,nurses,pharmacies]=await Promise.all([
      prefetched!==undefined?Promise.resolve(prefetched):request('/api/v1/admin/overview'),
      request('/api/v1/admin/nurses/pending'),
      request('/api/v1/admin/pharmacies/pending')
    ]);
    if(generation!==state.generation)return;
    const totalBookings=Array.isArray(overview.bookings)?overview.bookings.reduce((a,x)=>a+Number(x.count||0),0):0;
    const items=[
      ['المرضى النشطون',overview.activePatients,'♙'],
      ['الممرضون المعتمدون',overview.nursesApproved,'✚'],
      ['تمريض ينتظر القرار',overview.nursesPending,'◷'],
      ['الصيدليات المعتمدة',overview.pharmaciesApproved,'⌂'],
      ['صيدليات تنتظر القرار',overview.pharmaciesPending,'◌'],
      ['إجمالي الحجوزات',totalBookings,'◫']
    ];
    $('metrics').replaceChildren(...items.map(x=>metric(...x)));
    preview($('overviewNurses'),nurses,'ممرض');
    preview($('overviewPharmacies'),pharmacies,'صيدلية');
    setCounts(nurses.length,pharmacies.length);
    renderStatus($('statusBreakdown'),overview);
    $('lastUpdated').textContent='آخر تحديث: '+new Date().toLocaleTimeString('ar-EG',{hour:'numeric',minute:'2-digit'});
  }
  async function approveNurse(item) {
    if(!Array.isArray(item.skills)||!item.skills.length)throw new Error('لا يمكن اعتماد ممرض من غير مهارات.');
    if(!window.confirm('هل راجعت الترخيص والمهارات بنفسك؟ تأكيد اعتماد الممرض '+(item.fullName||'')+'؟'))return;
    await request('/api/v1/admin/nurses/'+encodeURIComponent(item.id)+'/approval',
      {method:'PATCH',data:{approved:true}});
    notify('تم اعتماد الممرض بنجاح.');
    await loadNurses();
  }
  async function loadNurses() {
    const items=await request('/api/v1/admin/nurses/pending');
    const target=$('nurseRequests');
    target.replaceChildren();
    setCounts(items.length,Number($('pharmacyNavCount').textContent)||0);
    if(!items.length)return empty(target,'كل طلبات التمريض تمت مراجعتها، لا توجد طلبات معلّقة.');
    for(const x of items) {
      const hasSkills=Array.isArray(x.skills)&&x.skills.length>0;
      const info=[
        'المدينة: '+(x.city||'غير محددة'),
        'الترخيص: '+(x.licenseNumber||'غير محدد'),
        'البريد: '+(x.email||'غير محدد'),
        'المهارات: '+(hasSkills?x.skills.join('، '):'لم تكتمل')
      ].join(' · ');
      const approve=actionButton('اعتماد الممرض',()=>approveNurse(x));
      approve.disabled=!hasSkills;approve.title=hasSkills?'راجع الترخيص قبل الاعتماد':'يجب إضافة مهارة أولًا';
      target.append(dataRow(x.fullName||'طلب ممرض',info,[approve]));
    }
  }
  async function approvePharmacy(item) {
    if(!item.hasAcceptedInvitation)throw new Error('لا يمكن الاعتماد قبل قبول دعوة الصيدلية.');
    if(!window.confirm('هل تحققت من ترخيص الصيدلية '+(item.businessName||'')+'، وتريد اعتمادها؟'))return;
    await request('/api/v1/admin/pharmacies/'+encodeURIComponent(item.id)+'/approval',
      {method:'PATCH',data:{approved:true}});
    notify('تم اعتماد الصيدلية بنجاح.');
    await loadPharmacies();
  }
  async function loadPharmacies() {
    const items=await request('/api/v1/admin/pharmacies/pending');
    const target=$('pharmacyRequests');
    target.replaceChildren();
    setCounts(Number($('nurseNavCount').textContent)||0,items.length);
    if(!items.length)return empty(target,'لا توجد صيدليات تنتظر الاعتماد.');
    for(const x of items) {
      const info=[
        'المدينة: '+(x.city||'غير محددة'),
        'الترخيص: '+(x.licenseNumber||'غير محدد'),
        'البريد: '+(x.email||'غير محدد'),
        x.hasAcceptedInvitation?'الدعوة مكتملة':'الدعوة لم تُقبل بعد'
      ].join(' · ');
      const approve=actionButton('اعتماد الصيدلية',()=>approvePharmacy(x));
      approve.disabled=!x.hasAcceptedInvitation;
      approve.title=x.hasAcceptedInvitation?'راجع الترخيص قبل الاعتماد':'الدعوة لم تُقبل بعد';
      target.append(dataRow(x.businessName||'طلب صيدلية',info,[approve]));
    }
  }
  async function setUserStatus(user) {
    const next=!user.isActive;
    const msg=next?'تفعيل':'تعطيل';
    if(!window.confirm('متأكد من '+msg+' حساب '+(user.fullName||user.email||'')+'؟'))return;
    await request('/api/v1/admin/users/'+encodeURIComponent(user.id)+'/status',
      {method:'PATCH',data:{isActive:next}});
    notify('تم '+msg+' الحساب.');
    await loadUsers();
  }
  async function loadUsers() {
    const items=await request('/api/v1/admin/users?page='+state.usersPage);
    const list=$('usersList');list.replaceChildren();
    if(!items.length)empty(list,'لا توجد حسابات في هذه الصفحة.');
    for(const x of items) {
      const roles=x.roles||[];
      const isAdmin=roles.includes('Admin');
      const s=x.isActive?'مفعل':'موقوف';
      const desc=[x.email||'',roleText(roles),s,x.isPendingInvitation?'بانتظار الدعوة':''].filter(Boolean).join(' · ');
      const acts=[];
      if(!isAdmin && !(x.isPendingInvitation&&!x.isActive))acts.push(
        actionButton(x.isActive?'إيقاف الحساب':'تفعيل الحساب',()=>setUserStatus(x),
          x.isActive?'action-btn danger':'action-btn')
      );
      list.append(dataRow(x.fullName||'مستخدم',desc,acts));
    }
    $('usersPage').textContent='صفحة '+state.usersPage;
    $('usersPrev').disabled=state.usersPage===1;
    $('usersNext').disabled=items.length<50;
  }
  async function loadAudit() {
    const items=await request('/api/v1/admin/audit?page='+state.auditPage);
    const target=$('auditList');target.replaceChildren();
    if(!items.length)empty(target,'لا توجد إجراءات مسجلة في هذه الصفحة.');
    for(const x of items) {
      const what=String(x.action||'إجراء إداري');
      const subtitle=[
        prettyDate(x.occurredAtUtc),
        x.entityType||'',
        x.entityId||''
      ].filter(Boolean).join(' · ');
      target.append(dataRow(what,subtitle));
    }
    $('auditPage').textContent='صفحة '+state.auditPage;
    $('auditPrev').disabled=state.auditPage===1;
    $('auditNext').disabled=items.length<50;
  }
  async function setView(view,prefetched) {
    if(!state.accessToken)return;
    if(!titles[view])return;
    state.view=view;const generation=++state.generation;
    $('pageTitle').textContent=titles[view][0];
    $('pageSubtitle').textContent=titles[view][1];
    for(const name of Object.keys(titles))$('view-'+name).hidden=name!==view;
    document.querySelectorAll('[data-section]').forEach(b=>{
      const active=b.dataset.section===view;
      b.classList.toggle('active',active);
      if(active)b.setAttribute('aria-current','page');else b.removeAttribute('aria-current');
    });
    clearError();$('loadingStatus').hidden=false;
    try{
      if(view==='overview')await loadOverview(prefetched);
      else if(view==='nurses')await loadNurses();
      else if(view==='pharmacies')await loadPharmacies();
      else if(view==='users')await loadUsers();
      else if(view==='audit')await loadAudit();
    }catch(e){
      if(generation===state.generation){showError(e.message||'تعذر تحديث البيانات.');notify(e.message,true);}
    }finally{if(generation===state.generation)$('loadingStatus').hidden=true;}
  }
  function init() {
    $('adminLoginForm').addEventListener('submit',login);
    $('adminFactorBack').addEventListener('click',()=>{
      setLoginFactor();
      $('loginError').hidden=true;
      $('adminPassword').focus();
    });
    $('togglePassword').addEventListener('click',()=>{
      const field=$('adminPassword'),btn=$('togglePassword'),show=field.type==='password';
      field.type=show?'text':'password';
      btn.textContent=show?'إخفاء':'إظهار';
      btn.setAttribute('aria-pressed',String(show));
      btn.setAttribute('aria-label',show?'إخفاء كلمة المرور':'إظهار كلمة المرور');
    });
    $('adminLogout').addEventListener('click',logout);
    $('refreshAdmin').addEventListener('click',()=>setView(state.view));
    document.querySelectorAll('[data-section]').forEach(b=>
      b.addEventListener('click',()=>setView(b.dataset.section)));
    document.querySelectorAll('[data-go]').forEach(b=>
      b.addEventListener('click',()=>setView(b.dataset.go)));
    for(const type of ['users','audit']){
      $(type+'Prev').addEventListener('click',()=>{
        if(state[type+'Page']>1)state[type+'Page']--;
        setView(type);
      });
      $(type+'Next').addEventListener('click',()=>{
        state[type+'Page']++;
        setView(type);
      });
    }
  }
  init();
})();
