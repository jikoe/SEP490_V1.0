"""Revise sequence notation using the video while retaining Report 3 flows."""
from pathlib import Path
import ast
import json
import re
import shutil

ROOT=Path(__file__).resolve().parents[1]
PREVIOUS=ROOT/'output/UC_UML_Video_Format'
OUT=ROOT/'output/UC_UML_Both_Videos'
OUT.mkdir(exist_ok=True)
tree=ast.parse((ROOT/'tools/build_uc_uml.py').read_text(encoding='utf-8'))
env={'MODELS':[]}
for node in tree.body:
    take=(isinstance(node,ast.FunctionDef) and node.name in ('model','C'))
    take|=(isinstance(node,ast.Assign) and any(isinstance(t,ast.Name) and t.id in ('USER','AUDIT') for t in node.targets))
    take|=(isinstance(node,ast.Expr) and isinstance(node.value,ast.Call) and isinstance(node.value.func,ast.Name) and node.value.func.id=='model')
    if take:exec(compile(ast.Module(body=[node],type_ignores=[]),'original-model-data','exec'),env)

STYLE='''skinparam backgroundColor white
skinparam defaultFontName Arial
skinparam defaultFontSize 14
skinparam shadowing false
skinparam roundcorner 0
skinparam ArrowColor #222222
skinparam sequenceMessageAlign left
skinparam maxMessageSize 240
skinparam ParticipantPadding 24
skinparam BoxPadding 10
skinparam sequence {
 ActorBorderColor #4F626C
 ActorBackgroundColor #78C9ED
 LifeLineBorderColor #777777
 LifeLineBackgroundColor #78C9ED
 ParticipantBorderColor #4F626C
 ParticipantBackgroundColor #78C9ED
 GroupBorderColor #333333
 GroupBackgroundColor white
 GroupHeaderFontColor #222222
 ActivationBorderColor #4F798C
 ActivationBackgroundColor #78C9ED
}
hide footbox
'''

QUERY_LABELS={
 'Matching account information':'getUsers(criteria, page)',
 'Page of account records':'users: List<User>',
 'Status becomes Suspended':'suspend(reason)',
 'Status becomes Active':'activate(reason)',
 'Clear lock; failedLoginCount = 0':'unlock()',
 'Matching categories':'getCategories(criteria, page)',
 'Category list':'categories: List<Category>',
 'Active products in category':'getActiveProducts(categoryId)',
 'Recheck active products':'getActiveProducts(categoryId)',
 'Create / update category':'createOrUpdateCategory(values)',
 'Update isActive':'setActive(active)',
 'Current stock':'getAvailableQuantity()',
 'Available quantities':'availableQuantities',
 'Selected items and quantities':'getSelectedItems()',
 'Selected CartItems with batch references':'selectedItems: List<CartItem>',
 'Remove only successfully ordered items':'removeOrderedItems(placedItems)',
 'Current stock for all selected batches':'getAvailableQuantities(selectedItems)',
 'Current batch availability':'getAvailableQuantities(selectedItems)',
 'Matching promotions':'getPromotions(criteria)',
 'Matching vouchers':'getVouchers(criteria)',
 'Matching tiers':'getSubscriptionPackages(criteria)',
 'Update availability for purchase':'setActive(active)',
 'Current details / defaults':'getDetailsOrDefaults(itemId)',
 'Registered account for reset email':'findByEmail(email)',
 'Update passwordHash':'updatePasswordHash(hash)',
 'Mark used; invalidate code':'markUsed(); invalidate()',
 'Consume reset authorization':'consume()',
 'Matching complaints':'getComplaints(criteria)',
 'Related order information':'getOrderDetails(orderId)',
 'Status = RefundPending':'markRefundPending()',
 'Retain Delivered':'retainStatus(Delivered)',
 'Release any unsettled seller payment':'releaseIfEligible()',
 'Release unsettled seller payment':'releaseIfEligible()',
 'Check 60-second cooldown':'canResend(now)',
 'Create / update tier':'createOrUpdatePackage(values)',
}

def prepare(m):
    raw=re.sub(r'"([^"]*)"',lambda hit:hit.group(0).replace('\n',r'\n'),m['seq'])
    lines=raw.splitlines()
    decl=[];body=[];roles={};types={};names={};merged=set()
    for line in lines:
        hit=re.match(r'^(actor|participant) "([^"]+)" as (\w+)',line)
        if not hit:body.append(line);continue
        kind,label,alias=hit.groups()
        if kind=='actor':roles[alias]='actor';names[alias]=label;types[alias]='Actor';continue
        st=re.search(r'«([^»]+)»',label)
        stereotype=st.group(1) if st else ''
        typ=re.search(r':\s*(\w+)',label)
        typ=typ.group(1) if typ else label.split(r'\n')[-1].strip()
        types[alias]=typ
        if stereotype=='business logic' or (m['code']=='UC-ADM-01' and alias=='Q'):
            merged.add(alias);continue
        if stereotype in ('coordinator','timer'):roles[alias]='control'
        elif stereotype in ('user interaction','proxy'):roles[alias]='boundary'
        elif stereotype=='external system':roles[alias]='external'
        else:roles[alias]='entity'
        if alias=='UI':names[alias]='frm'+typ.removesuffix('UI')
        elif roles[alias]=='control':names[alias]='ctr'+typ.removesuffix('Coordinator').removesuffix('Timer')
        elif roles[alias]=='entity':names[alias]='e_'+typ+': '+typ
        else:names[alias]=typ
    # Policy algorithms are self-messages on the primary control, matching BCE.
    # Their entity queries are retained and now originate from that control.
    clean=[];note=False
    for line in body:
        if line.strip().startswith('note ') and ':' in line:continue
        if line.strip().startswith('note '):note=True
        if note:
            if line.strip()=='end note':note=False
            continue
        if line.strip().startswith('ref '):continue
        hit=re.match(r'^(\s*)(\w+) ->> (\w+) : (.*)$',line)
        if hit:
            space,a,b,label=hit.groups()
            if a in merged and b=='CO':continue
            a='CO' if a in merged else a;b='CO' if b in merged else b
            label=QUERY_LABELS.get(label,label)
            # This is a field-format check only; control still validates all rules.
            if a=='UI' and b=='CO' and re.search(r'Save request|Reset submission|Confirmed decision|Confirmed order request|Recalculate checkout',label,re.I):
                clean.append(f'{space}UI ->> UI : validateFieldFormats()')
            clean.append(f'{space}{a} ->> {b} : {label}')
        else:
            line=re.sub(r'^(\s*(?:alt|else|opt|loop)) \[([^\n]*)\]$',r'\1 \2',line)
            clean.append(line)
    # Keep participant order Actor -> main UI -> controls -> entities -> external API.
    order=sorted(roles,key=lambda a:({'actor':0,'boundary':1 if a=='UI' else 3,'control':2,'entity':4,'external':5}[roles[a]],list(roles).index(a)))
    for a in order:
        kind='participant' if roles[a]=='external' else roles[a]
        decl.append(f'{kind} "{names[a]}" as {a} #78C9ED')
    return decl,clean,roles,types

def format_sequence(m):
    decl,body,roles,types=prepare(m)
    messages={}
    for i,line in enumerate(body):
        hit=re.match(r'^(\s*)(\w+) ->> (\w+) : (.*)$',line)
        if hit:messages[i]=hit.groups()
    # Determine structured-fragment ranges for asynchronous timeout processing.
    stack=[];ranges=[]
    for i,line in enumerate(body):
        s=line.strip()
        if re.match(r'^(alt|opt|loop|group)\b',s):stack.append(i)
        elif s=='end' and stack:ranges.append((stack.pop(),i))
    assert not stack,m['code']
    intervals={a:[] for a,r in roles.items() if r not in ('actor','boundary')}
    brief=set();selfcalls=set();kinds={}
    for i,(space,a,b,label) in messages.items():
        assert a in roles and b in roles,(m['code'],a,b)
        ra,rb=roles[a],roles[b]
        if a==b:
            kinds[i]='call';selfcalls.add(i);continue
        reply=(ra=='entity' and rb=='control') or (ra=='external') or (a=='GP' and rb=='control') or (a=='OTP' and b=='CO')
        if a=='T':kinds[i]='async'
        elif reply:kinds[i]='reply'
        else:kinds[i]='call'
        if reply or rb in ('actor','boundary'):continue
        limit=next((j for j,(_,x,y,_) in messages.items() if j>i and x==a and y==b),len(body))
        replies=[j for j,(_,x,y,_) in messages.items() if i<j<limit and x==b and y==a]
        if m['code']=='UC-CS-01' and a=='UI' and b=='CO' and label=='Information request':
            end=next((j for j,(_,x,y,_) in messages.items() if j>i and x=='CO' and y=='N'),i)
        elif a=='T':
            enclosing=[(s,e) for s,e in ranges if s<i<e]
            end=min(enclosing,key=lambda r:r[1]-r[0])[1]-1 if enclosing else i
        else:end=max(replies) if replies else i
        if end>i:intervals[b].append((i,end))
        else:brief.add(i)
    starts={};ends={}
    for a,vals in intervals.items():
        union=[]
        for s,e in sorted(vals):
            if union and s<=union[-1][1]:union[-1]=(union[-1][0],max(union[-1][1],e))
            else:union.append((s,e))
        for s,e in union:starts.setdefault(s,[]).append(a);ends.setdefault(e,[]).append(a)
    prefix={};counts={};event=0;current='1';numbered=[]
    def next_number(base):
        counts[base]=counts.get(base,0)+1
        return base+'.'+str(counts[base])
    out=['@startuml',STYLE,f'mainframe sd {m["code"]} - {m["title"]}',*decl,'activate A #78C9ED','activate UI #78C9ED']
    for i,line in enumerate(body):
        if i not in messages:
            out.append(line)
        else:
            space,a,b,label=messages[i]
            if roles[a]=='actor' or a=='T':
                event+=1;current=str(event);prefix={'UI':current,'CO':current,'OTP':current};number=current
            elif a=='UI':
                number=next_number(current)
                if roles[b]=='control':prefix[b]=number
            elif roles[a]=='control' or roles[b]=='control':
                control=a if roles[a]=='control' else b
                number=next_number(prefix.get(control,current))
                if roles[a]=='control' and roles[b]=='control' and a!=b and kinds[i]!='reply':prefix[b]=number
                elif roles[a]=='control' and roles[b]=='boundary' and b!='UI':prefix[b]=number
            elif a in prefix or b in prefix:
                number=next_number(prefix.get(a,prefix.get(b,current)))
            else:number=next_number(current)
            arrow={'call':'->','reply':'-->>','async':'->>'}[kinds[i]]
            out.append(f'{space}{a} {arrow} {b} : {number}: {label}')
            for alias in starts.get(i,[]):out.append(f'{space}activate {alias} #78C9ED')
            if i in selfcalls:
                out.extend([f'{space}activate {b} #78C9ED',f'{space}||8||',f'{space}deactivate {b}'])
            elif i in brief:
                out.extend([f'{space}activate {b} #78C9ED',f'{space}||8||',f'{space}deactivate {b}'])
            numbered.append(dict(number=number,sender=a,receiver=b,type=kinds[i],label=label))
        for alias in ends.get(i,[]):out.append(f'deactivate {alias}')
    out+=['deactivate UI','deactivate A','@enduml','']
    # Activation syntax is balanced even when alternatives are represented together.
    bal={}
    for line in out:
        h=re.match(r'\s*(activate|deactivate) (\w+)',line)
        if h:
            act,a=h.groups();bal[a]=bal.get(a,0)+(1 if act=='activate' else -1)
            assert bal[a]>=0,(m['code'],a,line)
    assert all(v==0 for v in bal.values()),(m['code'],bal)
    assert not any(roles[x['sender']]=='actor' and roles[x['receiver']]=='entity' for x in numbered)
    assert not any(x['sender']=='UI' and roles[x['receiver']]=='entity' for x in numbered)
    return '\n'.join(out),dict(code=m['code'],participants=[dict(alias=a,role=r,className=types[a]) for a,r in roles.items()],messages=numbered,balancedActivations=True)

checks=[]
for m in env['MODELS']:
    p=OUT/m['code'];p.mkdir(exist_ok=True)
    for ext in ('puml','svg','png'):shutil.copyfile(PREVIOUS/m['code']/f'class.{ext}',p/f'class.{ext}')
    seq,check=format_sequence(m)
    (p/'sequence.puml').write_text(seq,encoding='utf-8');checks.append(check)
for f in ('manifest.json','SOURCE_EXCERPTS.md','REVIEW_VIDEO.md','method-design.json'):
    shutil.copyfile(PREVIOUS/f,OUT/f)
(OUT/'SEQUENCE_DESIGN_CHECKS.json').write_text(json.dumps(checks,indent=2,ensure_ascii=False),encoding='utf-8')
review='''# Rà soát 8 sequence diagram theo video Hoàng Trang Channel

Ngày sửa: 16/10/2026. Video: https://www.youtube.com/watch?v=0JRT2mMvaww

## Các bước và hình thức đã đối chiếu

| Bước | Mốc video | Thay đổi áp dụng |
|---|---|---|
| Đọc UC, xác định luồng chính và thay thế | 00:15-02:52; 25:03-26:54 | Giữ các luồng từ Report 3; không thay nghiệp vụ bằng ví dụ đăng nhập. |
| Xác định Actor, Boundary, Control, Entity | 03:24-08:04; hình ký hiệu 05:23 | Actor ở trái, UI dạng boundary, điều phối dạng control, dữ liệu dạng entity; dùng biểu tượng tròn màu xanh như hình mẫu. |
| Đường sống và thanh kích hoạt | 02:57-04:48; 08:10-08:26 | Lifeline nét đứt; thanh kích hoạt xanh tại khoảng xử lý; activation lồng cho self-message. Thanh của UI thể hiện phiên tương tác trong generic view, không phải khóa DB hay giữ một HTTP request. |
| Phân biệt lời gọi đồng bộ / bất đồng bộ | 15:48-17:09 | Gọi xử lý/đọc dữ liệu: nét liền, đầu tam giác đặc. Tín hiệu hết hạn chứng cứ từ timer: nét liền, đầu mở. |
| Reply/return và self-message | 19:29-21:35 | Entity trả kết quả: nét đứt đầu mở; kết quả Gmail/OTP cũng là reply. Thông báo hiển thị là message thường. Policy validation và phép tính nội bộ là self-message của control. |
| Tạo đối tượng | 17:09-18:33 | Khi cần tạo bản ghi, message ghi rõ tạo đối tượng/bản ghi trên lifeline truy cập entity đã tồn tại. Không đồng nhất tạo record DB với tạo mới đối tượng truy cập dữ liệu; vì thế không ép lifeline entity repository phải bắt đầu giữa hình. |
| Dựng alt, opt, loop và guards | 22:14-24:41; 37:43-43:35 | Giữ điều kiện của luồng tương ứng, khung lồng đúng phạm vi và ngăn cách nhánh bằng nét đứt. Không áp số lần lặp ví dụ của video vào UC không có quy tắc đó. |
| Đánh số và sắp xếp tương tác | 29:31-29:57; 39:32; hình hoàn chỉnh 43:35 | Thông điệp actor 1,2,...; xử lý bên trong dùng 1.1, 1.1.1,...; thứ tự thời gian từ trên xuống; toàn hình có khung sd. |

## Quyết định mô hình

Bản trước là generic analysis sequence với simple messages theo Gomaa, chưa khẳng định sync/async và chưa có activation. Bản này cụ thể hóa ký pháp theo video: BCE, calls, replies, activation và numbering. Các policy/application-service lifeline được gom vào control; thông điệp tới entity vẫn giữ. Bảng `SEQUENCE_DESIGN_CHECKS.json` lưu thứ tự và loại từng thông điệp để truy vết.

Các entity lifeline là đối tượng cung cấp/truy cập dữ liệu đã hiện hữu trong ngữ cảnh UC. Ví dụ `e_Order: Order` trong checkout tiếp nhận yêu cầu truy vấn order trùng và tạo bản ghi mới; không phải một order mới đã tồn tại trước Place Order. Các thao tác tạo bản ghi được ghi rõ trong message. Phân biệt này tránh vẽ create-lifeline cho đối tượng vừa được truy vấn ở đầu UC.

Gmail là external participant; GmailProxy là boundary tích hợp, không phải UI. EvidenceDeadline là control phát tín hiệu bất đồng bộ. Không có đường trực tiếp Actor–Entity hay UI–Entity. Kiểm tra format ở UI không thay thế kiểm tra quyền, trạng thái và quy tắc nghiệp vụ ở control.

Tên getter/query bổ sung là đề xuất giao diện truy cập dữ liệu, không phải khẳng định API có sẵn trong Report 3. Class diagrams nghiệp vụ vẫn giữ nguyên bản ba ngăn đã sửa theo video TITV; UI/control và các truy vấn collection thuộc view tương tác bổ sung. Không gán toàn bộ truy vấn DB vào một đối tượng entity đơn lẻ khi triển khai.

Không thêm đường gạch X chỉ vì hình kết thúc: hủy record nghiệp vụ và hủy vòng đời object là hai việc khác nhau. Không sao chép quy tắc tài khoản/password dạng minh họa của video vào hệ thống.

## Lưu ý nghiệp vụ giữ nguyên

- Checkout: AF1-4, kiểm tra giá/tồn, chống trùng và kết quả đặt hàng nguyên tử; kết thúc tại chuyển sang payment.
- Reset: include AUTH-08, OTP 5 phút, resend 60 giây, vô hiệu sau hơn 5 lần sai và reset context đã xác minh.
- Complaint: quyết định hoàn tiền chỉ tạo manual RefundRequest; Finance chuyển tiền ở workflow khác; không tự restock hay sửa reputation. Quy tắc số tiền khi một bên quá hạn vẫn chưa đủ chi tiết.
- Dashboard: read-only, auto-refresh mỗi 60 giây; dữ liệu thiếu chỉ báo lỗi từng phần.
- Quy tắc delete/archive promotion/tier và các bất nhất mã UC tiếp tục được ghi trong tài liệu đối chiếu class và SOURCE_EXCERPTS.md.

## Đối chiếu từng UC

'''
for m,c in zip(env['MODELS'],checks):
    review+=f'### {m["code"]} — {m["title"]}\n\nNguồn: {m["section"]}, mã SRS {m["source"]}. Phạm vi: {m["trace"]}\n\n'
    review+='Participants: '+', '.join(f'{x["alias"]} ({x["role"]}: {x["className"]})' for x in c['participants'])+'.\n\n'
    review+=f'Tổng thông điệp sau khi gom policy vào control: {len(c["messages"])}. Các activation cân bằng; main/alternative flows được bảo toàn.\n\n'
(OUT/'REVIEW_SEQUENCE_VIDEO.md').write_text(review,encoding='utf-8')
print('Created eight revised BCE sequence sources; eight previous class diagrams preserved:',OUT)
