"""Revise the eight domain class views to the three-compartment video format."""
import ast
import json
from pathlib import Path
import shutil

ROOT=Path(__file__).resolve().parents[1]
OLD=ROOT/'output/UC_UML_Gomaa'
OUT=ROOT/'output/UC_UML_Video_Format'
OUT.mkdir(exist_ok=True)

# Load only the original model declarations, without running its file writer.
tree=ast.parse((ROOT/'tools/build_uc_uml.py').read_text(encoding='utf-8'))
env={'MODELS':[]}
for node in tree.body:
    if isinstance(node,ast.FunctionDef) and node.name in ('model','C'):
        exec(compile(ast.Module(body=[node],type_ignores=[]),'model-definitions','exec'),env)
    elif isinstance(node,ast.Assign) and any(isinstance(t,ast.Name) and t.id in ('USER','AUDIT') for t in node.targets):
        exec(compile(ast.Module(body=[node],type_ignores=[]),'model-constants','exec'),env)
    elif isinstance(node,ast.Expr) and isinstance(node.value,ast.Call) and isinstance(node.value.func,ast.Name) and node.value.func.id=='model':
        exec(compile(ast.Module(body=[node],type_ignores=[]),'model-data','exec'),env)

MODELS=env['MODELS']

METHODS={
'UC-ADM-03':{
 'User':['canBeManagedBy(actor: User): Boolean','suspend(reason: String): void','activate(reason: String): void','unlock(): void','isTemporarilyLocked(): Boolean'],
 'AuditLog':['recordChange(actorId: String, targetId: String, reason: String): void','getChangeDetails(): String'],
},
'UC-ADM-05':{
 'Category':['Category(name: String)','updateDetails(name: String, description: String): void','setParent(parent: Category): void','setImage(image: ImageReference): void','setPriority(priority: Integer): void','setActive(active: Boolean): void','canDelete(): Boolean','delete(): Boolean'],
 'Product':['getStatus(): ListingStatus','getCategory(): Category','reassignCategory(category: Category): void'],
 'AuditLog':['recordChange(actorId: String, targetId: String, reason: String): void','getChangeDetails(): String'],
},
'UC-RC-02':{
 'User':['getSelectedCart(): Cart','getDeliveryAddresses(): List<DeliveryAddress>','getRewardAccount(): RewardAccount'],
 'Cart':['getSelectedItems(): List<CartItem>','removeOrderedItems(items: List<CartItem>): void'],
 'CartItem':['getQuantity(): Decimal','getSelectedBatch(): HarvestBatch'],
 'Product':['getCurrentPrice(): Decimal','isAvailable(): Boolean'],
 'HarvestBatch':['isAvailable(quantity: Decimal): Boolean','reserve(quantity: Decimal): Boolean','releaseReservation(quantity: Decimal): void'],
 'DeliveryAddress':['validate(): Boolean','isServiceable(method: DeliveryMethod): Boolean','calculateDeliveryFee(method: DeliveryMethod): Decimal'],
 'Voucher':['isApplicable(subtotal: Decimal, at: DateTime): Boolean','calculateDiscount(subtotal: Decimal): Decimal'],
 'RewardAccount':['getAvailablePoints(): Integer','calculateDiscount(points: Integer): Decimal'],
 'Order':['Order(requestKey: String)','addItem(item: OrderItem): void','calculateTotal(): Decimal','matchesRequest(requestKey: String): Boolean','markPendingPayment(): void','getPaymentDestination(): String'],
 'OrderItem':['OrderItem(batch: HarvestBatch, quantity: Decimal, unitPrice: Decimal)','calculateSubtotal(): Decimal'],
},
'UC-AUTH-07':{
 'User':['getRegisteredEmail(): String','updatePasswordHash(hash: String): void'],
 'EmailOtp':['EmailOtp(email: String, expiresAt: DateTime)','isValid(code: String, at: DateTime): Boolean','recordFailedAttempt(): void','canResend(at: DateTime): Boolean','markUsed(): void','invalidate(): void'],
 'ResetAuthorization':['isValidFor(userId: String, purpose: OtpPurpose): Boolean','consume(): void'],
 'EmailVerificationService':['requestCode(email: String): Boolean','verifyCode(email: String, code: String): ResetAuthorization','resendCode(email: String): Boolean'],
},
'UC-CS-01':{
 'DisputeTicket':['getEvidence(): List<ComplaintEvidence>','requestInformation(party: Party): EvidenceRequest','validateResolution(decision: Resolution, amount: Decimal): Boolean','resolve(decision: Resolution, reason: String): void','markRefundPending(amount: Decimal): void','rejectClaim(reason: String): void'],
 'ComplaintEvidence':['getReference(): String','getSubmittedBy(): String'],
 'EvidenceRequest':['EvidenceRequest(party: Party, deadline: DateTime)','isOverdue(at: DateTime): Boolean','hasResponded(): Boolean','recordResponse(at: DateTime): void'],
 'Order':['getTotalAmount(): Decimal','getStatus(): OrderStatus','markRefundPending(): void'],
 'RefundRequest':['RefundRequest(amount: Decimal)','getStatus(): RefundStatus','getRequestedAmount(): Decimal'],
 'FarmerPayout':['isUnsettled(): Boolean','releaseIfEligible(): Boolean'],
 'Notification':['Notification(recipientId: String, content: String)','getDeliveryStatus(): NotificationStatus'],
 'AuditLog':['recordChange(actorId: String, targetId: String, reason: String): void','recordEvidenceOverdue(ticketId: String, at: DateTime): void'],
},
'UC-PS-01':{
 'Promotion':['Promotion(name: String)','validateDetails(): Boolean','updateDetails(rule: DiscountRule, eligibility: EligibilityRule): void','setValidity(startAt: DateTime, endAt: DateTime): void','setUsageLimit(limit: Integer): void','setActive(active: Boolean): void','canRemove(): Boolean','removeOrArchive(): Boolean'],
 'Voucher':['Voucher(code: String)','validateDetails(): Boolean','updateDiscount(type: DiscountType, value: Decimal): void','setValidity(startAt: DateTime, endAt: DateTime): void','setUsageLimit(limit: Integer): void','setActive(active: Boolean): void','canRemove(): Boolean','removeOrArchive(): Boolean'],
 'AuditLog':['recordChange(actorId: String, targetId: String, reason: String): void','getChangeDetails(): String'],
},
'UC-ADM-01':{
 'DashboardQuery':['getSummary(range: DateRange): DashboardSummary','getPendingWork(): List<PendingWorkItem>','getDetailDestination(item: PendingWorkItem): String','refresh(range: DateRange): DashboardSummary'],
 'DashboardSummary':['getGmv(): Decimal','getTotalOrders(): Integer','getDeliverySuccessRate(): Decimal','getCancellationRate(): Decimal','getUserGrowth(role: Role): Integer','getTotalPendingWork(): Integer'],
 'PendingWorkItem':['getWaitTime(at: DateTime): Duration','isSlaOverdue(at: DateTime): Boolean','getDetailDestination(): String'],
 'Order':['getAmount(): Decimal','getStatus(): OrderStatus'],
 'User':['getRole(): Role','getCreatedAt(): DateTime'],
 'KycApplication':['isPending(): Boolean','getSubmittedAt(): DateTime'],
 'Product':['isPendingModeration(): Boolean','getSubmittedAt(): DateTime'],
 'DisputeTicket':['isPendingResolution(): Boolean','getSubmittedAt(): DateTime'],
},
'UC-ADM-09':{
 'B2BSubscriptionPackage':['B2BSubscriptionPackage(name: String)','validateDetails(): Boolean','updateDetails(name: String, price: Decimal, durationDays: Integer): void','setBenefits(benefits: BenefitSet): void','setUsageLimits(limits: UsageLimits): void','setActive(active: Boolean): void','canRemove(): Boolean','removeOrArchive(): Boolean'],
 'B2BSubscription':['getPackage(): B2BSubscriptionPackage','getStatus(): SubscriptionStatus','isActive(at: DateTime): Boolean'],
 'AuditLog':['recordChange(actorId: String, targetId: String, reason: String): void','getChangeDetails(): String'],
}}

RELATIONS={
'UC-ADM-03':[
 'User "1" -- "0..*" AuditLog : target / account history',
],
'UC-ADM-05':[
 'Category "0..1" -- "0..*" Category : parent / children',
 'Category "1" -- "0..*" Product : classifies',
 'Category "1" -- "0..*" AuditLog : category change history',
],
'UC-RC-02':[
 'User "1" -- "0..1" Cart : owns',
 'User "1" -- "0..*" DeliveryAddress : saves',
 'User "1" -- "0..1" RewardAccount : has',
 'User "1" -- "0..*" Order : places',
 'Cart "1" *-- "0..*" CartItem : contains',
 'CartItem "0..*" -- "1" HarvestBatch : selected batch',
 'Product "1" -- "0..*" HarvestBatch : offered batches',
 'Order "1" *-- "1..*" OrderItem : contains',
 'OrderItem "0..*" -- "1" HarvestBatch : reserved batch',
 'Order "0..*" -- "0..1" Voucher : applies',
 'Order ..> DeliveryAddress : copies address',
 'Order ..> RewardAccount : reads eligible points',
],
'UC-AUTH-07':[
 'User "1" -- "0..*" EmailOtp : receives reset code',
 'User "1" -- "0..*" ResetAuthorization : reset permission',
 'EmailOtp "1" -- "0..1" ResetAuthorization : establishes',
 'EmailVerificationService ..> EmailOtp : issues / verifies',
 'EmailVerificationService ..> ResetAuthorization : issues',
],
'UC-CS-01':[
 'Order "1" -- "0..*" DisputeTicket : has complaint',
 'DisputeTicket "1" *-- "0..*" ComplaintEvidence : contains evidence',
 'DisputeTicket "1" *-- "0..*" EvidenceRequest : requests information',
 'DisputeTicket "1" -- "0..1" RefundRequest : requests manual refund',
 'Order "1" -- "0..*" FarmerPayout : seller payouts',
 'DisputeTicket "1" -- "0..*" AuditLog : decision history',
 'DisputeTicket "1" -- "0..*" Notification : case notifications',
],
'UC-PS-01':[
 'Promotion "0..1" -- "0..*" AuditLog : promotion changes',
 'Voucher "0..1" -- "0..*" AuditLog : voucher changes',
],
'UC-ADM-01':[
 'DashboardQuery ..> DashboardSummary : returns',
 'DashboardQuery ..> PendingWorkItem : projects',
 'DashboardQuery ..> Order : reads order metrics',
 'DashboardQuery ..> User : reads user growth',
 'DashboardQuery ..> KycApplication : reads pending KYC',
 'DashboardQuery ..> Product : reads pending listings',
 'DashboardQuery ..> DisputeTicket : reads pending cases',
],
'UC-ADM-09':[
 'B2BSubscriptionPackage "1" -- "0..*" B2BSubscription : purchased package',
 'B2BSubscriptionPackage "1" -- "0..*" AuditLog : package change history',
]}

EXTRA={
'UC-RC-02':[('User','entity',['userId: String','fullName: String','status: AccountStatus'])],
'UC-AUTH-07':[('EmailVerificationService','service',['otpValidityMinutes: Integer = 5','resendCooldownSeconds: Integer = 60','maxFailedAttempts: Integer = 5'])]
}

EXTRA_ATTR={
 ('UC-ADM-01','DashboardQuery'):['selectedRange: DateRange','lastRefreshedAt: DateTime'],
 ('UC-ADM-01','User'):['createdAt: DateTime'],
}

STYLE='''!pragma layout smetana
skinparam backgroundColor white
skinparam defaultFontName Arial
skinparam defaultFontSize 14
skinparam shadowing false
skinparam roundcorner 0
skinparam classAttributeIconSize 0
skinparam classFontStyle bold
skinparam classBackgroundColor #FFFFCC
skinparam classBorderColor #8C8261
skinparam ArrowColor #665B4D
skinparam ArrowFontSize 12
skinparam titleFontSize 20
skinparam titleFontColor #222222
skinparam footerFontSize 11
skinparam nodesep 65
skinparam ranksep 85
hide circle
top to bottom direction
'''

analysis=['# Rà soát class diagram theo video TITV', '',
 'Ngày sửa: 16/10/2026. Nguồn nghiệp vụ: bản Report 3 đã tải ngày 15/10/2026, trích trong SOURCE_EXCERPTS.md.',
 'Video: [09 - Phân tích và vẽ sơ đồ lớp - CLASS DIAGRAM](https://www.youtube.com/watch?v=lT5A6-Lg_QI).', '',
 '## Các bước và định dạng đã đối chiếu', '',
 '| Nội dung video | Mốc thời gian | Áp dụng |', '|---|---|---|',
 '| Tên lớp, thuộc tính, phương thức là ba ngăn | 01:11-02:45; mẫu cuối 1:04:50 | Tất cả lớp có đúng ba ngăn; tên đậm, căn giữa; nền vàng nhạt, viền mảnh. |',
 '| Tìm danh từ quan trọng và loại lớp không phù hợp | 03:09-07:24; 32:04-39:10 | Chọn các lớp nghiệp vụ và lớp dịch vụ cần thiết; gộp thông tin mô tả thành thuộc tính. |',
 '| Tìm thuộc tính và kiểu dữ liệu | 39:10-48:48 | Dùng dữ liệu của UC, field descriptions và entity descriptions trong Report 3. |',
 '| Phạm vi truy cập | 43:28-45:03 | Thuộc tính `-name: Type`, phương thức `+method(parameter: Type): ReturnType`. Constructor không có kiểu trả về. |',
 '| Quan hệ, chiều ký hiệu, multiplicity | 07:27-15:53; 52:03-1:00:49 | Association nét liền; composition hình thoi đặc ở phía whole; dependency nét đứt mũi tên mở. Bản số theo nghiệp vụ. |',
 '| Suy phương thức từ động từ và use case | 49:13-52:03; 1:01:10-1:04:49 | Bổ sung hành vi thực tế cho từng lớp, đối chiếu trao đổi đã có trong sequence. |',
 '| Bố trí dễ đọc, hạn chế giao cắt | 1:04:50-1:05:16 | Xếp các lớp theo quan hệ, đường nối không xuyên hộp, tách ghi chú ra khỏi hình. |', '',
 '## Thay đổi so với bản trước', '',
 'Bản trước là analysis realization view theo Gomaa: có actor, UI/coordinator/policy, stereotype và trách nhiệm trong ngoặc nhọn, chưa thiết kế operation. Bản sửa này là domain/design class view theo ví dụ video: bỏ biểu tượng actor và nhãn stereotype trên các hộp; bỏ UI/coordinator/policy khỏi view nghiệp vụ; thay mô tả trách nhiệm bằng phương thức có tham số và kiểu trả về. Service chỉ giữ khi cần thể hiện hành vi chuyên trách (OTP, truy vấn dashboard). Đây là hai góc nhìn khác nhau; không có yêu cầu xóa các lớp UI/control khỏi hệ thống.', '',
 'Mỗi hình giữ đúng định dạng ba ngăn, kể cả khi có ít thuộc tính. Không thêm lớp con hay inheritance chỉ để đủ loại quan hệ: Report 3 quy định mỗi User có một role, chưa yêu cầu hệ phân cấp class theo role.', '',
 'Không sao chép quan hệ Customer–Order trong bài tập video vào AgriSustain: video minh họa composition, nhưng Report 3 yêu cầu lưu lịch sử và không xóa cứng User. Vì vậy User–Order là association 1–0..*. Order–OrderItem và Cart–CartItem mới dùng composition. Khối lượng/số tiền dùng Decimal, không bắt buộc dùng Double của bài ví dụ.', '',
 '## Phạm vi phương thức và các điểm chưa chốt', '',
 'Tên phương thức, chữ ký và một số quan hệ điều hướng là đề xuất thiết kế dựa trên UC; không phải tên API/phương thức có sẵn trong SRS. Các view của cùng một lớp chỉ hiện phần liên quan đến UC đang xét. Không tự thêm hàng loạt getter/setter hay public setter làm mất kiểm soát dữ liệu.', '',
 'Các kiểu như AccountStatus, OrderStatus, Role, Resolution là enumeration nghiệp vụ; Money được thể hiện bằng Decimal. DateRange, Address, BenefitSet, UsageLimits, ImageReference là kiểu giá trị. List<T> là tập hợp đối tượng. Đây không phải bảng PK/FK.', '',
 'AuditLog biểu diễn lịch sử đã lọc theo đối tượng của view. Ở promotion/voucher, mỗi record đích là một Promotion hoặc Voucher (XOR), không đồng thời cả hai; multiplicity 0..1 ở mỗi phía đích phản ánh điều đó.', '',
 'Quy tắc delete/archive promotion và tier, sửa tier đang có thuê bao, tỷ lệ dashboard, GMV khi partial refund, giữ điểm/quota voucher và kết quả khi một bên quá hạn chứng cứ vẫn cần chốt nghiệp vụ. Phương thức canRemove/removeOrArchive không tự quyết định quy tắc còn thiếu. Không đưa hành vi chuyển tiền của Finance vào Dispute Officer. Dữ liệu dashboard vẫn là read-only; không thêm setter cập nhật nguồn.', '',
 'Sequence diagram được giữ nguyên nội dung từ bộ 16 trang trước; đó là view tương tác có UI/coordinator. Bản sửa lần này chỉ thay tám class diagram theo yêu cầu. Các lớp hỗ trợ trong sequence không được hiểu là bị xóa khỏi thiết kế hệ thống.', '',
 '## Rà soát từng UC', '']

for m in MODELS:
    code=m['code']; folder=OUT/code; folder.mkdir(exist_ok=True)
    classes=[]
    candidates=list(m['classes'])+EXTRA.get(code,[])
    # Domain entities/value objects, plus the two purposeful services.
    for name,stereo,attrs in candidates:
        if name not in METHODS[code]:continue
        attrs=[a for a in attrs if not a.startswith('{')]
        attrs+=EXTRA_ATTR.get((code,name),[])
        attrs=[a.replace('Identifier','String').replace('Money','Decimal') for a in attrs]
        classes.append((name,attrs,METHODS[code][name]))
    assert {c[0] for c in classes}==set(METHODS[code]),code
    lines=['@startuml',STYLE,'title',f'{code} - {m["title"]}','Class Diagram','end title']
    for name,attrs,methods in classes:
        lines.append(f'class {name} {{')
        lines += ['  -'+a for a in attrs]
        lines += ['  --']
        lines += ['  +'+op for op in methods]
        lines += ['}']
    lines+=RELATIONS[code]
    lines += ['@enduml','']
    (folder/'class.puml').write_text('\n'.join(lines),encoding='utf-8')
    for ext in ['puml','svg','png']:shutil.copyfile(OLD/code/f'sequence.{ext}',folder/f'sequence.{ext}')
    analysis += [f'### {code} — {m["title"]}', '',f'Nguồn: {m["section"]} / {m["source"]}.', '',
                 '1. Danh từ/lớp giữ lại: '+', '.join(c[0] for c in classes)+'.',
                 '2. Thuộc tính: lấy từ UC, bảng field và entity tương ứng; thêm dấu `-` và chuẩn hóa kiểu.',
                 '3. Quan hệ: '+ '; '.join(RELATIONS[code])+'.',
                 '4. Phương thức: '+ '; '.join(f'{name}: '+', '.join(op.split('(')[0] for op in ops) for name,attrs,ops in classes)+'.', '',
                 'Ràng buộc tiếp tục áp dụng: '+m['constraints'].replace('\n',' '),'']

(OUT/'REVIEW_VIDEO.md').write_text('\n'.join(analysis),encoding='utf-8')
shutil.copyfile(OLD/'SOURCE_EXCERPTS.md',OUT/'SOURCE_EXCERPTS.md')
shutil.copyfile(OLD/'manifest.json',OUT/'manifest.json')
(OUT/'method-design.json').write_text(json.dumps(METHODS,ensure_ascii=False,indent=2),encoding='utf-8')
print('Created eight revised class sources and retained eight original sequence diagrams:',OUT)
