#!/usr/bin/env python3
"""Generate a source-derived OpenAPI review snapshot, Postman collection and environment.

Stdlib only. The running ASP.NET MapOpenApi() is authoritative for framework metadata;
export it separately with Export-LiveApi.ps1. This script intentionally does not
pretend that statically inferred success/response shapes were tested.
"""
import argparse
import json
import re
import sys
from collections import OrderedDict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CTRL = ROOT / 'src/DareCare.Api/Controllers'
OUT = ROOT / 'integration'
CONTRACTS = list((ROOT / 'src/DareCare.Application/Contracts').glob('*.cs')) + [ROOT / 'src/DareCare.Api/Contracts/UploadPrescriptionRequest.cs']
ENUMS = list((ROOT / 'src/DareCare.Domain').glob('*.cs'))
VERBS = {'HttpGet':'get','HttpPost':'post','HttpPut':'put','HttpPatch':'patch','HttpDelete':'delete'}
HTTP_ATTR = re.compile(r'\[(HttpGet|HttpPost|HttpPut|HttpPatch|HttpDelete)(?:\("([^"]*)"\))?\]')
METHOD_RE = re.compile(r'public\s+(?:async\s+)?(?:Task<IActionResult>|IActionResult)\s+(\w+)\s*\(', re.S)
GUID_EXAMPLE = '00000000-0000-0000-0000-000000000001'


def balanced(s, opening, start):
    level=0
    for i in range(start,len(s)):
        if s[i]==opening: level+=1
        elif s[i]==({ '(':')', '{':'}' }[opening]):
            level-=1
            if level==0: return s[start+1:i],i+1
    raise ValueError('Unbalanced '+opening)


def split_top(text):
    chunks=[]; current=''; depth=0
    for c in text:
        if c in '<([': depth+=1
        if c in '>)]': depth-=1
        if c==',' and depth==0:
            chunks.append(current.strip());current='';continue
        current+=c
    if current.strip(): chunks.append(current.strip())
    return chunks


def camel(s): return s[0].lower()+s[1:] if s else s


def parse_enums():
    out={}
    for f in ENUMS:
        txt=f.read_text(encoding='utf-8')
        m=re.search(r'public\s+enum\s+(\w+)\s*\{([^}]+)\}',txt,re.S)
        if m:
            vals=[v.strip().split('=')[0].strip() for v in m.group(2).split(',') if v.strip()]
            out[m.group(1)]={'type':'string','enum':vals}
    return out


def openapi_type(t,enums):
    t=t.strip().rstrip('?')
    if t in enums:return {'$ref':f'#/components/schemas/{t}'}
    if t in ('Guid',):return {'type':'string','format':'uuid'}
    if t in ('DateTime','DateTimeOffset',):return {'type':'string','format':'date-time'}
    if t in ('int','long','short'):return {'type':'integer'}
    if t in ('decimal','double','float'):return {'type':'number'}
    if t=='bool':return {'type':'boolean'}
    if t=='IFormFile':return {'type':'string','format':'binary'}
    if t.startswith('List<') or t.startswith('IReadOnlyList<'):
        inn=t[t.find('<')+1:t.rfind('>')]
        return {'type':'array','items':openapi_type(inn,enums)}
    if t in ('string','char'):return {'type':'string'}
    return {'type':'object'}


def parse_dtos(enums):
    dtos={}
    for f in CONTRACTS:
        txt=f.read_text(encoding='utf-8')
        for m in re.finditer(r'public\s+sealed\s+class\s+(\w+)\s*\{',txt):
            body,_=balanced(txt,'{',m.end()-1)
            props={}; req=[]
            for prop in re.finditer(r'((?:\s*\[[^\]]+\]\s*)*)public\s+([A-Za-z][\w<>?, ]*)\s+(\w+)\s*\{\s*get;\s*set;\s*\}',body,re.S):
                attr,t,name=prop.group(1),prop.group(2).strip(),prop.group(3)
                schema=openapi_type(t,enums)
                limit=re.search(r'StringLength\((\d+)',attr)
                if limit and schema.get('type')=='string':schema['maxLength']=int(limit.group(1))
                if 'EmailAddress' in attr:schema['format']='email'
                if '[Required' in attr:req.append(camel(name))
                if '?' in t:schema['nullable']=True
                props[camel(name)]=schema
            if props:dtos[m.group(1)]={'type':'object','properties':props,**({'required':req} if req else {})}
        for m in re.finditer(r'public\s+sealed\s+record\s+(\w+)\s*\(',txt):
            args,_=balanced(txt,'(',m.end()-1)
            props={};req=[]
            for part in split_top(args):
                items=part.strip().split()
                if len(items)>=2:
                    t,name=items[0],items[1]
                    props[camel(name)]=openapi_type(t,enums)
                    if not t.endswith('?'):req.append(camel(name))
            if props:dtos[m.group(1)]={'type':'object','properties':props,'required':req}
    return dtos


def parameters(signature,enums):
    result=[]; body=None; multipart=False
    for item in split_top(signature):
        m=re.match(r'\s*(?:\[(FromBody|FromForm|FromQuery|FromRoute)[^\]]*\]\s*)?([\w<>?,]+)\s+(\w+)',item)
        if not m:continue
        origin,t,name=m.groups()
        if origin in ('FromBody','FromForm'):
            body=t.rstrip('?');multipart=(origin=='FromForm')
        elif origin=='FromQuery':
            result.append({'name':name,'in':'query','required':False,'schema':openapi_type(t,enums)})
    return result,body,multipart


def enumerate_routes(enums):
    entries=[]
    for f in sorted(CTRL.glob('*Controller.cs')):
        txt=f.read_text(encoding='utf-8')
        cl=re.search(r'public\s+sealed\s+class\s+\w+Controller\b',txt)
        if not cl:continue
        top=txt[:cl.start()]
        cr=re.findall(r'\[Route\("([^"]+)"\)\]',top)
        if not cr:continue
        prefix=cr[-1].strip('/')
        cls_roles=re.findall(r'AppRoles\.(\w+)', '\n'.join(re.findall(r'\[Authorize[^\]]*\]',top)))
        cls_auth='[Authorize' in top
        hits=list(HTTP_ATTR.finditer(txt[cl.end():]))
        for i,h in enumerate(hits):
            offset=cl.end()
            tail=txt[offset+h.end():offset+(hits[i+1].start() if i+1<len(hits) else len(txt))]
            mm=METHOD_RE.search(tail)
            if not mm:raise ValueError('Method not found for '+f.name+' '+h.group(0))
            attrs=tail[:mm.start()]
            auth_attr='[Authorize' in attrs
            allow_anon='[AllowAnonymous]' in attrs
            roles=re.findall(r'AppRoles\.(\w+)', '\n'.join(re.findall(r'\[Authorize[^\]]*\]',attrs)))
            roles=roles if roles else cls_roles
            protected=not allow_anon and (auth_attr or cls_auth)
            meth_name=mm.group(1)
            sig,pos=balanced(tail,'(',mm.end()-1)
            query,body,multipart=parameters(sig,enums)
            path=(prefix+'/'+(h.group(2) or '').strip('/')).rstrip('/')
            if not path.startswith('/'):path='/'+path
            path=re.sub(r'\{(\w+):[^}]+\}',r'{\1}',path)
            path_params=[]
            for name in re.findall(r'\{(\w+)\}',path):
                path_params.append({'name':name,'in':'path','required':True,'schema':{'type':'string','format':'uuid'}})
            entries.append({ 'file':f.name, 'method':VERBS[h.group(1)], 'path':path,
                'name':meth_name, 'protected':protected,'roles':sorted(set(roles)) if protected else [],
                'params':path_params+query, 'body':body,'multipart':multipart })
    entries.sort(key=lambda x:(x['path'],x['method']))
    dup=[(a['method'],a['path']) for a,b in zip(entries,entries[1:]) if (a['method'],a['path'])==(b['method'],b['path'])]
    if dup:raise ValueError('Route collision: '+str(dup))
    return entries


REQUEST_EXAMPLES={
 'RegisterPatientRequest':{'fullName':'Demo Patient','email':'{{patientEmail}}','password':'{{patientPassword}}','phoneNumber':'+201000000001'},
 'RegisterNurseRequest':{'fullName':'Demo Nurse','email':'{{nurseEmail}}','password':'{{nursePassword}}','phoneNumber':'+201000000002','licenseNumber':'DEMO-123456','yearsOfExperience':4},
 'LoginRequest':{'email':'{{email}}','password':'{{password}}'},
 'RefreshRequest':{'refreshToken':'{{refreshToken}}'},
 'CreateBookingRequest':{'nurseProfileId':'{{nurseProfileId}}','availabilitySlotId':'{{availabilitySlotId}}','careServiceId':'{{careServiceId}}','addressId':'{{addressId}}'},
 'SavePatientAddressRequest':{'label':'Home','city':'Mansoura','fullAddress':'DEMO ADDRESS - Example St. 10','notes':'Synthetic address only','isDefault':True},
 'UpdateAccountRequest':{'fullName':'Demo Patient','phoneNumber':'+201000000001'},
 'UpdatePatientProfileRequest':{'city':'Mansoura','defaultAddress':'DEMO ADDRESS','emergencyContactName':'Demo Contact','emergencyContactPhone':'+201000000000'},
 'UpdateNurseProfileRequest':{'bio':'Demo nurse','city':'Mansoura','serviceArea':'Mansoura','yearsOfExperience':4,'hourlyRate':250},
 'SetNurseSkillsRequest':{'skillIds':['{{skillId}}']},
 'CreateAvailabilitySlotRequest':{'startsAt':'2026-11-15T10:00:00Z','endsAt':'2026-11-15T11:30:00Z'},
 'CreatePharmacyRequest':{'email':'{{pharmacyEmail}}','contactName':'Demo Pharmacist','phoneNumber':'+201000000003','businessName':'DEMO Pharmacy','licenseNumber':'DEMO-PHARM-123','city':'Mansoura','address':'Synthetic Demo Street 1'},
 'UpdatePharmacyRequest':{'businessName':'DEMO Pharmacy','city':'Mansoura','address':'Synthetic Address'},
 'QuotePrescriptionRequest':{'amount':150,'notes':'Synthetic demo quote'},
 'SetApprovalRequest':{'approved':True},
 'ChangeUserStatusRequest':{'isActive':True},
 'ChangePasswordRequest':{'currentPassword':'{{oldPassword}}','newPassword':'{{newPassword}}'},
 'EmailRequest':{'email':'{{patientEmail}}'},
 'EmailTokenRequest':{'email':'{{patientEmail}}','code':'{{emailCode}}'},
 'ResetPasswordRequest':{'email':'{{patientEmail}}','code':'{{emailCode}}','newPassword':'{{newPassword}}'},
 'CreateNurseReviewRequest':{'rating':5,'comment':'Synthetic demo review'},
 'CreateSupportTicketRequest':{'subject':'Synthetic question about booking','category':'Booking','message':'This is a test ticket containing no patient data.'},
 'ReplySupportTicketRequest':{'message':'Synthetic demo support reply.'},
 'UpdateSupportStatusRequest':{'status':'InProgress'},
}


def postman_sample(t,dtos):
    if t in REQUEST_EXAMPLES:return REQUEST_EXAMPLES[t]
    if t not in dtos:return {}
    o={}
    for k,s in dtos[t]['properties'].items():
        if s.get('nullable') and k not in dtos[t].get('required',[]):continue
        if s.get('format')=='uuid':o[k]='{{'+k+'}}'
        elif s.get('format')=='email':o[k]='{{email}}'
        elif s.get('format')=='date-time':o[k]='2026-11-15T10:00:00Z'
        elif s.get('type')=='integer':o[k]=1
        elif s.get('type')=='number':o[k]=100
        elif s.get('type')=='boolean':o[k]=True
        elif s.get('type')=='array':o[k]=[]
        elif s.get('type')=='string':o[k]='Demo value'
    return o


def get_openapi(entries,dtos,enums):
    paths=OrderedDict()
    for e in entries:
        p=paths.setdefault(e['path'],{})
        name=e['name'];verb=e['method'];component=e['file'].removesuffix('Controller.cs')
        op={'tags':[component], 'summary':f'{component}: {name}',
            'operationId':component+'_'+name,
            'description':'Source-derived operation listing. Export /openapi/v1.json from running API for framework-authoritative schema. ' +
                (('Allowed roles: '+', '.join(e['roles'])+'.') if e['roles'] else ('Requires bearer token.' if e['protected'] else 'Public endpoint.')),
            'x-darecare-roles':e['roles'] if e['roles'] else (['Authenticated'] if e['protected'] else ['Public']),
            'parameters':e['params'],
            'responses':{'200':{'description':'Success. HTTP status and response shape depend on controller branch.'},
                '400':{'description':'Malformed request / validation error.'},
                **({'401':{'description':'Missing / expired access token.'},'403':{'description':'Insufficient role or object-level permission.'}} if e['protected'] else {})},
        }
        if e['protected']:op['security']=[{'BearerAuth':[]}]
        if e['body']:
            if e['multipart']:
                op['requestBody']={'required':True,'content':{'multipart/form-data':{'schema':{'$ref':f"#/components/schemas/{e['body']}"}}}}
            elif e['body'] in dtos:
                op['requestBody']={'required':True,'content':{'application/json':{'schema':{'$ref':f"#/components/schemas/{e['body']}"}}}}
        p[verb]=op
    paths['/health']={'get':{'summary':'Liveness','tags':['Health'],'responses':{'200':{'description':'Kestrel is running.'}}}}
    paths['/health/ready']={'get':{'summary':'Database readiness','tags':['Health'],'responses':{'200':{'description':'Connected to database.'},'503':{'description':'Database unavailable.'}}}}
    return {'openapi':'3.0.3',
        'info':{'title':'DareCare API v1 — source-derived team handoff','version':'1.0.0',
                'description':'Generated from controllers, no live .NET execution. Runtime /openapi/v1.json is the authoritative generated schema.'},
        'servers':[{'url':'http://127.0.0.1:5208','description':'Local demo only — change to actual HTTPS staging URL when deployed.'}],
        'paths':paths,
        'components':{'securitySchemes':{'BearerAuth':{'type':'http','scheme':'bearer','bearerFormat':'JWT'}},
                      'schemas':{**enums,**dtos}}
    }


def choose_group(e):
    if not e['protected']:return 'Public'
    if e['file']=='AuthController.cs':return 'Authentication'
    if len(e['roles'])==1:return e['roles'][0]
    return 'Shared (authenticated)'


def postman_collection(entries,dtos):
    folders=OrderedDict((name,[]) for name in ['Start Here','Authentication','Public','Patient','Nurse','Pharmacy','Admin','Shared (authenticated)'])
    intro={'name':'GET readiness', 'request':{'method':'GET','header':[],
        'url':{'raw':'{{baseUrl}}/health/ready','host':['{{baseUrl}}'],'path':['health','ready']}},
        'response':[]}
    folders['Start Here'].append(intro)
    def login_variant(role):
        k=role.lower()
        body={'email':'{{'+k+'Email}}','password':'{{'+k+'Password}}'}
        test=f'''pm.test("Login success", function () {{ pm.response.to.have.status(200); }});\nif (pm.response.code === 200) {{ const json=pm.response.json(); pm.environment.set("{k}Token",json.accessToken); pm.environment.set("{k}RefreshToken",json.refreshToken); }}'''
        return {'name':'Login as '+role,'event':[{'listen':'test','script':{'type':'text/javascript','exec':test.splitlines()}}],
            'request':{'method':'POST','header':[{'key':'Content-Type','value':'application/json'}],
                'auth':{'type':'noauth'},
                'body':{'mode':'raw','raw':json.dumps(body,ensure_ascii=False,indent=2),'options':{'raw':{'language':'json'}}},
                'url':{'raw':'{{baseUrl}}/api/v1/auth/login','host':['{{baseUrl}}'],'path':['api','v1','auth','login']}},'response':[]}
    for role in ['Patient','Nurse','Pharmacy','Admin']:
        folders['Authentication'].append(login_variant(role))
    variables={
        'id':'{{id}}', 'nurseId':'{{nurseProfileId}}', 'nurseProfileId':'{{nurseProfileId}}',
        'slotId':'{{availabilitySlotId}}','bookingId':'{{bookingId}}',
        'addressId':'{{addressId}}', 'pharmacyId':'{{pharmacyProfileId}}',
        'pharmacyProfileId':'{{pharmacyProfileId}}', 'prescriptionId':'{{prescriptionId}}',
        'serviceId':'{{careServiceId}}', 'userId':'{{userId}}',
    }
    for e in entries:
        path=e['path']
        # Generic {id} varies by resource. Use context-specific Postman environment variables.
        if '{id}' in path:
            route_var=(
                'bookingId' if path.startswith('/api/v1/bookings/') else
                'prescriptionId' if path.startswith('/api/v1/prescriptions/') else
                'orderId' if path.startswith('/api/v1/orders/') else
                'ticketId' if path.startswith('/api/v1/support/tickets/') else
                'reviewId' if path.startswith('/api/v1/reviews/') else
                'notificationId' if path.startswith('/api/v1/notifications/') else 'id')
            path=path.replace('{id}', '{{'+route_var+'}}')
        path=re.sub(r'(?<!\{)\{(\w+)\}(?!\})',lambda m:variables.get(m.group(1),'{{'+m.group(1)+'}}'),path)

        query=[{'key':p['name'],'value':'','disabled':True} for p in e['params'] if p['in']=='query']
        role=e['roles'][0] if e['roles'] else ('Patient' if e['protected'] else 'Public')
        if 'Admin' in e['roles']:role='Admin'
        # First role is alphabetic in the spec, explicit per-resource folders are preferred.
        token=role.lower()+'Token'
        qstring='' if not query else '?'+'&'.join(p['key']+'=' for p in query)
        request={'method':e['method'].upper(), 'header':[{'key':'Accept','value':'application/json'}],
            'url':{'raw':'{{baseUrl}}'+path+qstring, 'host':['{{baseUrl}}'],
                   'path':[p for p in path.lstrip('/').split('/') if p], **({'query':query} if query else {})},
            'description':('Roles: '+(', '.join(e['roles']) if e['roles'] else ('Any authenticated' if e['protected'] else 'Public')))
        }
        if e['protected']:request['auth']={'type':'bearer','bearer':[{'key':'token','value':'{{'+token+'}}','type':'string'}]}
        else:request['auth']={'type':'noauth'}
        if e['body']:
            if e['multipart']:
                request['body']={'mode':'formdata','formdata':[
                    {'key':'pharmacyProfileId','value':'{{pharmacyProfileId}}','type':'text'},
                    {'key':'bookingId','value':'{{bookingId}}','type':'text','disabled':True},
                    {'key':'notes','value':'Synthetic test only','type':'text'},
                    {'key':'file','type':'file','src':''}]}
            else:
                request['header'].append({'key':'Content-Type','value':'application/json'})
                sample=postman_sample(e['body'],dtos)
                request['body']={'mode':'raw','raw':json.dumps(sample,indent=2,ensure_ascii=False), 'options':{'raw':{'language':'json'}}}
        item={'name':e['method'].upper()+' '+e['path'], 'request':request,'response':[]}
        if e['file']=='AuthController.cs' and e['name']=='Login':
            # Generic login request is superseded by per-role presets above, no duplicate needed.
            continue
        folders[choose_group(e)].append(item)
    return {'info':{'name':'DareCare API — Flutter & Web v1',
        'description':'Source-derived (not yet runtime-verified). Import environment and fill only your LOCAL demo credentials. Never share patient data or tokens. Run role-specific login requests to set JWT variables.',
        'schema':'https://schema.getpostman.com/json/collection/v2.1.0/collection.json'},
        'item':[{'name':k,'item':v} for k,v in folders.items() if v],
        'variable':[{'key':'serviceApiVersion','value':'v1'}]}


def environment():
    vars={'baseUrl':'http://127.0.0.1:5208'}
    for role in ['patient','nurse','pharmacy','admin']:
        for suffix in ['Email','Password','Token','RefreshToken']:
            vars[role+suffix]=''
    for k in ['id','nurseProfileId','availabilitySlotId','careServiceId','bookingId','pharmacyProfileId','prescriptionId','addressId','ticketId','orderId','reviewId','notificationId','skillId','userId','emailCode','oldPassword','newPassword','refreshToken']:
        vars[k]=''
    def field_type(name):
        # Mark credential fields as masked in Postman, but keep their exported values EMPTY.
        return 'secret' if any(s in name.lower() for s in ['password','token','code']) else 'default'
    return {'name':'DareCare - Local only (no secrets)','values':[{'key':k,'value':v,'enabled':True,'type':field_type(k)} for k,v in vars.items()],
            '_postman_variable_scope':'environment'}


def manifest(e):
    return {'version':'v1','source':'ASP.NET C# controller route attributes',
            'count':len(e),'endpoints':[{'method':x['method'].upper(),'path':x['path'],'roles':x['roles'] or (['Authenticated'] if x['protected'] else ['Public'])} for x in e]}


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--check',action='store_true',help='Fail if checked-in handoff files differ from source')
    args=ap.parse_args()
    enums=parse_enums(); dtos=parse_dtos(enums); entries=enumerate_routes(enums)
    if not 70<=len(entries)<=150:raise RuntimeError(f'Unexpected number of routes: {len(entries)}')
    OUT.mkdir(exist_ok=True)
    outputs={
        'DareCare_OpenAPI_SourceSnapshot.json':get_openapi(entries,dtos,enums),
        'DareCare_Postman_Collection.json':postman_collection(entries,dtos),
        'DareCare_Local.postman_environment.json':environment(),
        'DareCare_API_Endpoints.json':manifest(entries),
    }
    mismatches=[]
    for fname,obj in outputs.items():
        desired=json.dumps(obj,indent=2,ensure_ascii=False)+'\n'
        target=OUT/fname
        if args.check:
            if not target.exists() or target.read_text(encoding='utf-8')!=desired:mismatches.append(fname)
        else:target.write_text(desired,encoding='utf-8')
    print(f'Parsed {len(entries)} controller endpoints, {len(dtos)} DTOs, {len(enums)} enums; ' + ('check' if args.check else 'files generated'))
    if mismatches:
        print('Outdated: '+', '.join(mismatches),file=sys.stderr)
        return 1
    return 0

if __name__=='__main__':raise SystemExit(main())
