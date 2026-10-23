"""Build traceable, local-only COMET analysis models for eight Report 3 UCs."""
from pathlib import Path
from zipfile import ZipFile
from lxml import etree
from html import escape
import json
import re

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'output' / 'UC_UML_Gomaa'
OUT.mkdir(parents=True, exist_ok=True)
NS = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
doc = etree.fromstring(ZipFile(ROOT / 'tmp/report3_uc_current.docx').read('word/document.xml'))
tables = doc.xpath('//w:tbl', namespaces=NS)

def table_text(index):
    return '\n'.join(' | '.join(' '.join(c.xpath('.//w:t/text()', namespaces=NS)) for c in row.xpath('./w:tc', namespaces=NS)) for row in tables[index].xpath('./w:tr', namespaces=NS))

COMMON = '''skinparam backgroundColor white
skinparam defaultFontName Arial
skinparam defaultFontSize 14
skinparam shadowing false
skinparam roundcorner 0
skinparam ArrowColor #334155
skinparam classBorderColor #334155
skinparam classBackgroundColor #FFFFFF
skinparam classAttributeIconSize 0
skinparam stereotypeCBackgroundColor #FFFFFF
skinparam NoteBackgroundColor #FFFBEA
skinparam NoteBorderColor #BFA35A
skinparam noteFontSize 12
skinparam titleFontSize 20
skinparam titleFontColor #163D32
skinparam footerFontSize 11
'''

MODELS = []
def model(code, source, title, section, ti, actor, ui, ctl, classes, relations, constraints, seq, trace, decisions):
    MODELS.append(dict(code=code, source=source, title=title, section=section, table=ti, actor=actor,
                       ui=ui, ctl=ctl, classes=classes, relations=relations,
                       constraints=constraints, seq=seq.strip(), trace=trace, decisions=decisions))

def C(name, stereotype, *attributes):
    return (name, stereotype, attributes)

AUDIT = C('AuditLog', 'entity', 'action: String', 'actorId: Identifier', 'targetId: Identifier', 'occurredAt: DateTime', 'beforeState: String', 'afterState: String', 'reason: String')
USER = C('User', 'entity', 'userId: Identifier', 'name: String', 'email: String', 'role: Role', 'status: AccountStatus', 'lockUntil: DateTime [0..1]', 'failedLoginCount: Integer')

model('UC-ADM-03', 'UC-ADM-03', 'Manage User Account Status', 'II.4.2', 51,
      'System Administrator', 'UserManagementUI', 'AccountStatusCoordinator', [
          USER, AUDIT,
          C('AccountActionPolicy', 'business logic', '{authorization, eligibility, self-action guard}'),
      ], [
          'UserManagementUI ..> AccountStatusCoordinator : account requests',
          'AccountStatusCoordinator ..> AccountActionPolicy : validate action',
          'AccountStatusCoordinator ..> User : find / update',
          'AccountStatusCoordinator ..> AuditLog : record confirmed action',
          'User "1" -- "0..*" AuditLog : target account / history',
      ], '''GBR-06: actorId != targetUserId for suspend, activate and unlock.
GBR-07: status change requires reason + actor + time + before/after.
Unlock clears temporary lock and failed-login count; status is separate.
Cancel: no account change and no audit record.''', '''
actor "System Administrator" as A
participant "«user interaction»\n: UserManagementUI" as UI
participant "«coordinator»\n: AccountStatusCoordinator" as CO
participant "«business logic»\n: AccountActionPolicy" as P
participant "«entity»\nusers: User [*]" as U
participant "«entity»\n: AuditLog" as L
A ->> UI : Open User Management / search / filter / page
UI ->> CO : Account query(criteria, page)
CO ->> U : Matching account information
U ->> CO : Page of account records
CO ->> UI : Records and pagination
alt [no matching accounts]
  UI ->> A : MSG01; preserve criteria
else [matching accounts]
  UI ->> A : User list
end
opt [AF1: View Details]
  A ->> UI : Select account details
  UI ->> CO : Account detail request(userId)
  CO ->> U : Identity, role, status and permitted KYC information
  U ->> CO : Account details
  CO ->> UI : Details
  UI ->> A : Display account details
end
opt [suspend / activate / unlock selected]
  A ->> UI : Select action(targetUserId)
  UI ->> A : Confirmation; reason for status change
  alt [AF3: Cancel]
    A ->> UI : Cancel action
    UI ->> A : Unchanged account; no audit entry
  else [Confirm]
    A ->> UI : Confirm(action, reason)
    UI ->> CO : Confirmed account action
    CO ->> P : Authorization and eligibility check(actor, target, action, reason)
    P ->> CO : Validation result
    alt [not authorized / self-action / invalid target or reason]
      CO ->> UI : Action rejected; no change
      UI ->> A : Validation explanation
    else [eligible]
      alt [Suspend: target Active]
        CO ->> U : Status becomes Suspended
      else [Activate: target Suspended]
        CO ->> U : Status becomes Active
      else [Unlock: target temporarily locked]
        CO ->> U : Clear lock; failedLoginCount = 0
      end
      CO ->> L : Actor, target, action, time, before/after and reason
      CO ->> U : Latest account list
      U ->> CO : Updated page
      CO ->> UI : Success and latest records
      UI ->> A : Confirmation and refreshed list
    end
  end
end
''', 'NF1-7; AF1-4; III.4.1.1-4.1.2; GBR-02,05-08.',
      'AccountActionPolicy là lớp phân tích được suy ra từ GBR-06/07; AuditLog được yêu cầu trực tiếp. Không mô hình hóa Administrator thành bảng riêng. Đọc KYC vẫn phải tuân GBR-08.')

model('UC-ADM-05', 'UC-ADM-05', 'Manage Product Categories', 'II.4.3', 52,
      'System Administrator', 'CategoryManagementUI', 'CategoryCoordinator', [
          C('Category', 'entity', 'categoryId: Identifier', 'name: String', 'description: String [0..1]', 'image: ImageReference [0..1]', 'priority: Integer = 0', 'isActive: Boolean'),
          C('Product', 'entity', 'productId: Identifier', 'name: String', 'status: ListingStatus'),
          C('CategoryPolicy', 'business logic', '{validate name, image, priority, parent and deletion}'), AUDIT,
      ], [
          'CategoryManagementUI ..> CategoryCoordinator : category requests',
          'CategoryCoordinator ..> CategoryPolicy : validate',
          'CategoryPolicy ..> Category : check siblings and parent',
          'CategoryCoordinator ..> Category : query / maintain',
          'CategoryCoordinator ..> Product : check active products',
          'CategoryCoordinator ..> AuditLog : record confirmed action',
          'Category "0..1" -- "0..*" Category : parent / children',
          'Category "1" -- "0..*" Product : classifies',
      ], '''Name: 2..100 characters; unique ignoring case within the same parent.
Description <= 500 characters; image JPG/JPEG/PNG <= 2 MB.
Priority >= 0; parent may be empty (root).
GBR-10: category containing active products cannot be deleted.
Reassignment/archive behavior for other references needs clarification.''', '''
actor "System Administrator" as A
participant "«user interaction»\n: CategoryManagementUI" as UI
participant "«coordinator»\n: CategoryCoordinator" as CO
participant "«business logic»\n: CategoryPolicy" as P
participant "«entity»\ncategories: Category [*]" as C
participant "«entity»\nproducts: Product [*]" as PR
participant "«entity»\n: AuditLog" as L
A ->> UI : Open / search / filter / page categories
UI ->> CO : Category query(criteria, page)
CO ->> C : Matching categories
C ->> CO : Category list
CO ->> UI : List and current criteria
alt [AF4: no results]
  UI ->> A : MSG01; preserve filters
else [results]
  UI ->> A : Category list
end
alt [New Category / Edit]
  A ->> UI : Select New or Edit
  UI ->> CO : Category details / available active parents
  CO ->> C : Detail information
  C ->> CO : Existing values / parent options
  CO ->> UI : Category form
  UI ->> A : Name, parent, description, image, priority, status
  alt [AF1: Cancel]
    A ->> UI : Cancel
    UI ->> A : Return to list; no save
  else [Save]
    A ->> UI : Submit category values
    UI ->> CO : Save request
    CO ->> P : Validate fields, sibling uniqueness and parent
    P ->> C : Existing sibling names / parent information
    C ->> P : Matching names / parent
    P ->> CO : Validation result
    alt [invalid]
      CO ->> UI : Field errors
      UI ->> A : Correct values and resubmit
    else [valid; AF3 permits empty parent]
      CO ->> C : Create / update category
      CO ->> L : Category change and acting administrator
      CO ->> UI : Saved category and refreshed list
      UI ->> A : Success
    end
  end
else [AF2: Delete]
  A ->> UI : Select Delete
  UI ->> CO : Deletion eligibility(categoryId)
  CO ->> PR : Active products in category
  PR ->> CO : Active-product count
  alt [contains active products]
    CO ->> UI : Reject deletion under GBR-10
    UI ->> A : Explain required category/product changes
  else [eligible under GBR-10]
    CO ->> UI : Eligible for confirmation
    UI ->> A : Request deletion confirmation
    alt [confirmed]
      A ->> UI : Confirm Delete
      UI ->> CO : Confirmed deletion
      CO ->> PR : Recheck active products
      PR ->> CO : Current eligibility
      alt [still eligible]
        CO ->> C : Delete subject to reference-integrity policy
        CO ->> L : Confirmed deletion
        CO ->> UI : Success and refreshed list
      else [now ineligible]
        CO ->> UI : Deletion rejected; category unchanged
      end
    else [cancelled]
      A ->> UI : Cancel; category unchanged
    end
  end
else [AF5: Activate / Deactivate]
  A ->> UI : Select status change
  UI ->> A : Confirmation
  opt [confirmed]
    A ->> UI : Confirm
    UI ->> CO : Confirmed status change
    CO ->> C : Update isActive
    CO ->> L : Status change
    CO ->> UI : Refreshed list
    UI ->> A : Confirmation
  end
end
''', 'NF1-8; AF1-5; III.4.2.1-4.2.2; GBR-09/10.',
      'CategoryPolicy là lớp suy ra để gom quy tắc. Bổ sung kiểm tra cây không có chu trình là đề xuất cần xác nhận, chưa coi là quy tắc đã có. Không suy diễn xóa dây chuyền Product/Category con; sách không bắt buộc composition cho cây danh mục.')

model('UC-RC-02', 'UC-CON-02', 'Checkout and Place Retail Order', 'II.6.2', 61,
      'Consumer', 'RetailCheckoutUI', 'CheckoutCoordinator', [
          C('Cart', 'entity', 'cartId: Identifier', 'ownerId: Identifier'),
          C('CartItem', 'entity', 'quantity: Decimal', 'selected: Boolean'),
          C('Product', 'entity', 'productId: Identifier', 'currentUnitPrice: Money', 'unit: String'),
          C('HarvestBatch', 'entity', 'batchCode: String', 'availableQuantity: Decimal', 'reservedQuantity: Decimal'),
          C('DeliveryAddress', 'entity', 'recipientName: String', 'phone: String', 'province: String', 'district: String', 'ward: String', 'addressDetail: String'),
          C('Voucher', 'entity', 'code: String', 'discountType: DiscountType', 'discountValue: Decimal', 'maxDiscount: Money [0..1]', 'minOrderAmount: Money', 'usageLimit: Integer', 'usedCount: Integer', 'startAt: DateTime', 'endAt: DateTime', 'isActive: Boolean'),
          C('RewardAccount', 'entity', 'ownerId: Identifier', 'availablePoints: Integer'),
          C('Order', 'entity', 'orderCode: String', 'consumerId: Identifier', 'requestKey: String', 'status: OrderStatus', 'deliveryAddressSnapshot: Address', 'deliveryMethod: DeliveryMethod', 'paymentMethod: PaymentMethod', 'subtotal: Money', 'deliveryFee: Money', 'voucherDiscount: Money', 'pointDiscount: Money', 'totalAmount: Money', 'notes: String [0..1]'),
          C('OrderItem', 'entity', 'quantity: Decimal', 'unitPriceSnapshot: Money'),
          C('CheckoutPolicy', 'business logic', '{validate stock, current price, address, discounts}', '{calculate final amount}'),
      ], [
          'RetailCheckoutUI ..> CheckoutCoordinator : checkout requests',
          'CheckoutCoordinator ..> CheckoutPolicy : validate / recalculate',
          'CheckoutCoordinator ..> Cart : selected items / remove placed items',
          'CheckoutCoordinator ..> Order : duplicate lookup / create',
          'CheckoutCoordinator ..> HarvestBatch : reserve ordered quantities',
          'CheckoutPolicy ..> Product : current price / availability',
          'CheckoutPolicy ..> HarvestBatch : stock availability',
          'CheckoutPolicy ..> DeliveryAddress : serviceability',
          'CheckoutPolicy ..> Voucher : eligibility / discount',
          'CheckoutPolicy ..> RewardAccount : eligible points',
          'Cart "1" *-- "0..*" CartItem : contains',
          'CartItem "0..*" -- "1" HarvestBatch : selected batch',
          'Product "1" -- "0..*" HarvestBatch : offered batches',
          'Order "1" *-- "1..*" OrderItem : ordered lines',
          'OrderItem "0..*" -- "1" HarvestBatch : reserves from',
          'Order "0..*" -- "0..1" Voucher : applied voucher',
      ], '''Total = subtotal + deliveryFee - voucherDiscount - pointDiscount.
Each ordered line refers to one HarvestBatch (Report 3 I.3.2).
Stock is reserved at batch level; revalidate on Place Order.
Duplicate request returns the existing PendingPayment order.
Checkout ends at payment navigation, before gateway settlement.''', '''
actor "Consumer" as A
participant "«user interaction»\n: RetailCheckoutUI" as UI
participant "«coordinator»\n: CheckoutCoordinator" as CO
participant "«business logic»\n: CheckoutPolicy" as P
participant "«entity»\n: Cart" as C
participant "«entity»\nbatches: HarvestBatch [*]" as B
participant "«entity»\norders: Order [*]" as O
note over P
Policy consults Product, DeliveryAddress,
Voucher and RewardAccount (see class view).
end note
A ->> UI : Checkout selected cart items
UI ->> CO : Checkout information request
CO ->> C : Selected items and quantities
C ->> CO : Selected CartItems with batch references
CO ->> P : Current prices, addresses and eligible points
P ->> B : Current stock
B ->> P : Available quantities
P ->> CO : Initial checkout information
CO ->> UI : Items, address options, points and totals
UI ->> A : Display checkout
loop [until accepted summary or consumer returns to cart]
  A ->> UI : Address, delivery method, payment method, optional voucher / points / notes
  UI ->> CO : Recalculate checkout
  CO ->> P : Validate stock, prices, address, voucher and points
  P ->> B : Current batch availability
  B ->> P : Stock information
  P ->> CO : Validation result and calculated summary
  alt [AF1: invalid voucher]
    CO ->> UI : MSG116; discount removed; recalculated total
    UI ->> A : Select another voucher or continue without voucher
  else [AF2: unserviceable address]
    CO ->> UI : MSG117
    UI ->> A : Select or enter valid address
  else [AF3: stock unavailable / price changed]
    CO ->> UI : MSG88 / MSG118 with affected items
    UI ->> A : Update items and restart checkout
  else [valid]
    CO ->> UI : Recalculated summary
    UI ->> A : Review final amount
  end
end
alt [Return to Cart]
  A ->> UI : Return to Cart
  UI ->> A : Cart; no order created
else [Place Order after accepted valid summary]
  A ->> UI : Place Order
  UI ->> CO : Confirmed order request(requestKey, acceptedSummary)
  CO ->> O : Existing order for consumer and requestKey
  O ->> CO : Existing order / none
  alt [AF4: duplicate request]
    CO ->> UI : MSG119 and existing PendingPayment order
    UI ->> A : Existing order / payment navigation; no second reservation
  else [new request]
    CO ->> P : Revalidate confirmed information and current values
    P ->> B : Current stock for all selected batches
    B ->> P : Availability
    P ->> CO : Validation result
    alt [validation changed or invalid]
      CO ->> UI : Appropriate MSG88 / MSG116 / MSG117 / MSG118
      UI ->> A : Correct checkout; confirm updated summary
    else [valid]
      group One indivisible placement outcome (analysis constraint)
        CO ->> B : Reserve all selected quantities if still available
        B ->> CO : Reservation result
        alt [all reservations successful]
          CO ->> O : New order code, PendingPayment, lines and accepted snapshots
          CO ->> C : Remove only successfully ordered items
        else [reservation fails]
          CO ->> B : Undo any partial reservation
          CO ->> UI : MSG88; no order and no cart removal
        end
      end
      opt [placement successful]
        CO ->> UI : Order created and payment destination
        UI ->> A : Continue to payment UC (identifier discrepancy in source)
      end
    end
  end
end
note over CO,O
Design constraint: uniqueness and reservation/order/cart changes
must preserve all-or-nothing outcome under concurrent requests.
Point / voucher quota timing is not specified by Report 3.
end note
''', 'NF1-7; AF1-4; III.6.1.1; GBR-13..17; I.3.3 batch inventory / voucher rules.',
      'RewardAccount và requestKey là đề xuất cấu trúc để hiện thực hóa điểm và chống trùng, chưa phải entity/tên trường chính thức trong I.3.2. Snapshot địa chỉ/giá và tính nguyên tử là lựa chọn thiết kế bảo toàn yêu cầu. Không tự thêm giữ/trừ điểm hay quota voucher vì SRS chưa quy định thời điểm. Payment ở II.6.3 là UC-RC-03 nhưng field table gọi UC-CON-03; dùng nhãn payment UC. Không gọi PayOS trong checkout vì secondary actor của UC này là None.')

model('UC-AUTH-07', 'UC-AUTH-07', 'Reset Password', 'II.1.7 + II.1.8', 44,
      'Guest / Registered User', 'PasswordResetUI', 'PasswordResetCoordinator', [
          C('User', 'entity', 'userId: Identifier', 'email: String', 'passwordHash: String'),
          C('EmailOtp', 'entity', 'email: String', 'purpose: OtpPurpose', 'codeDigest: String', 'expiresAt: DateTime', 'failedAttempts: Integer', 'used: Boolean', 'lastSentAt: DateTime'),
          C('ResetAuthorization', 'entity', 'userId: Identifier', 'purpose: OtpPurpose', 'verified: Boolean', 'consumed: Boolean'),
          C('EmailOtpCoordinator', 'coordinator', '{realizes included UC-AUTH-08}'),
          C('GmailProxy', 'proxy', '{encapsulates Gmail Notification API}'),
          C('GmailNotificationAPI', 'external system', '{email delivery outcome}'),
          C('PasswordPolicy', 'business logic', '{validate policy and password confirmation}', '{secure password hashing}'),
      ], [
          'PasswordResetUI ..> PasswordResetCoordinator : reset request',
          'PasswordResetUI ..> EmailOtpCoordinator : code / resend input',
          'PasswordResetCoordinator ..> EmailOtpCoordinator : include Verify Email OTP',
          'PasswordResetCoordinator ..> PasswordPolicy : validate / hash',
          'PasswordResetCoordinator ..> User : registered account / password update',
          'PasswordResetCoordinator ..> ResetAuthorization : check / consume reset permission',
          'EmailOtpCoordinator ..> ResetAuthorization : issue on verification success',
          'EmailOtpCoordinator ..> EmailOtp : issue / validate / invalidate',
          'EmailOtpCoordinator ..> GmailProxy : send verification code',
          'GmailProxy ..> GmailNotificationAPI : notification API communication',
          'User "1" -- "0..*" ResetAuthorization : reset permissions',
          'EmailOtp "1" -- "0..1" ResetAuthorization : establishes',
      ], '''Included UC-AUTH-08: OTP valid 5 minutes; single use.
More than 5 incorrect attempts invalidates the OTP.
Resend after 60 seconds invalidates the previous code.
Only verified reset context may reach password update.
Cancel never changes password; successful reset redirects to Sign In.''', '''
actor "Guest / Registered User" as A
participant "«user interaction»\n: PasswordResetUI" as UI
participant "«coordinator»\n: PasswordResetCoordinator" as CO
participant "«coordinator»\n: EmailOtpCoordinator" as OTP
participant "«proxy»\n: GmailProxy" as GP
participant "«external system»\nGmail Notification API" as G
participant "«entity»\n: EmailOtp" as E
participant "«entity»\n: ResetAuthorization" as R
participant "«business logic»\n: PasswordPolicy" as P
participant "«entity»\n: User" as U
A ->> UI : Forgot Password
UI ->> A : Registered email form
A ->> UI : Email; Send Verification Code
UI ->> CO : Reset request(email)
CO ->> U : Registered account for reset email
U ->> CO : Account lookup result
note over CO,U : Reset requires a registered account; unknown-email response is unspecified.
opt [registered target and reset input accepted]
  group Included UC-AUTH-08: Verify Email OTP
    CO ->> OTP : Verify email request(email, purpose = PasswordReset)
    OTP ->> E : Generate single-use code; expiry = now + 5 minutes
    OTP ->> GP : Verification email
    GP ->> G : Send code
    G ->> GP : Delivery outcome
    GP ->> OTP : Delivery outcome
    alt [delivery failed]
      OTP ->> UI : MSG26 and recorded delivery error
      UI ->> A : Retry later; password form remains hidden
    else [delivery accepted]
      OTP ->> UI : MSG14; OTP input and resend control
      UI ->> A : Enter verification code
      loop [verification pending and user has not cancelled]
        alt [Resend selected]
          A ->> UI : Resend Code
          UI ->> OTP : Resend request
          OTP ->> E : Check 60-second cooldown
          E ->> OTP : Cooldown result
          alt [cooldown complete]
            OTP ->> E : Invalidate old code; generate new code (5 minutes)
            OTP ->> GP : New verification email
            GP ->> G : Send new code
            G ->> GP : Delivery outcome
            GP ->> OTP : Delivery outcome
            OTP ->> UI : MSG14 / MSG26 according to outcome
          else [cooldown not complete]
            OTP ->> UI : Resend unavailable until cooldown ends
          end
        else [Verify selected]
          A ->> UI : Code; Verify
          UI ->> OTP : Verification code
          OTP ->> E : Check correctness, expiry, unused state and attempts
          E ->> OTP : Verification result
          alt [incorrect / expired / already used]
            OTP ->> E : Record failed attempt; invalidate if expired or > 5 failures
            OTP ->> UI : Error; request a new code if invalidated
            UI ->> A : OTP input stays open
          else [valid]
            OTP ->> E : Mark used; invalidate code
            OTP ->> R : Verified, single-reset authorization
            OTP ->> CO : Verification success(resetContext)
          end
        end
      end
    end
  end
  opt [verification succeeded and not cancelled]
    CO ->> UI : Reset-password fields for verified email
    UI ->> A : Enter new password and confirmation
    loop [until valid password or cancellation]
      A ->> UI : New password; confirmation
      UI ->> CO : Reset submission(resetContext, password, confirmation)
      CO ->> R : Verify reset purpose, target and unused permission
      R ->> CO : Authorization result
      CO ->> P : Password-policy and confirmation validation
      P ->> CO : Valid / errors
      alt [invalid password / mismatch / invalid reset context]
        CO ->> UI : Validation errors; no password change
        UI ->> A : Correct values / restart verification
      else [valid and authorized]
        CO ->> P : Secure password hash
        P ->> CO : Hash
        CO ->> U : Update passwordHash
        CO ->> R : Consume reset authorization
        CO ->> UI : Password reset successful
        UI ->> A : Success; redirect to Sign In
      end
    end
  end
end
note over A,CO
Cancel at any pre-completion point returns to Sign In;
password remains unchanged. Successful reset exits retry loops.
end note
''', 'II.1.7 NF1-7 and all alternatives; II.1.8 UC-AUTH-08; III.1.4.',
      'EmailOtp và ResetAuthorization là cấu trúc phân tích suy ra, có thể lưu tạm trong cache. ResetAuthorization giải quyết việc AUTH-08 dùng và vô hiệu OTP trước khi AUTH-07 đổi mật khẩu; không kiểm tra lại một OTP đã dùng. Thời hạn context, phản hồi email không tồn tại và thu hồi phiên đăng nhập sau reset chưa được đặc tả.')

model('UC-CS-01', 'UC-ADM-06', 'Resolve Complaint', 'II.4.4', 53,
      'Dispute Officer', 'ComplaintResolutionUI', 'ComplaintCoordinator', [
          C('DisputeTicket', 'entity', 'ticketId: Identifier', 'description: String', 'status: ComplaintStatus', 'faultParty: FaultParty', 'resolution: Resolution [0..1]', 'resolutionNotes: String [0..1]', 'refundAmount: Money [0..1]', 'resolvedAt: DateTime [0..1]'),
          C('ComplaintEvidence', 'entity', 'evidenceReference: String', 'submittedBy: Identifier', 'submittedAt: DateTime'),
          C('EvidenceRequest', 'entity', 'requestedParty: Party', 'deadline: DateTime', 'respondedAt: DateTime [0..1]', 'status: EvidenceRequestStatus'),
          C('Order', 'entity', 'orderCode: String', 'status: OrderStatus', 'totalAmount: Money'),
          C('RefundRequest', 'entity', 'refundId: Identifier', 'amount: Money', 'status: RefundStatus', 'manualTransferReference: String [0..1]'),
          C('FarmerPayout', 'entity', 'payoutCode: String', 'status: PayoutStatus'),
          C('Notification', 'entity', 'recipientId: Identifier', 'content: String', 'deliveryStatus: NotificationStatus'),
          C('ComplaintResolutionPolicy', 'business logic', '{validate resolution, amount and evidence deadline}'),
          C('EvidenceDeadlineTimer', 'timer', '{activate evidence deadline evaluation}'), AUDIT,
      ], [
          'ComplaintResolutionUI ..> ComplaintCoordinator : case requests / confirmed decision',
          'ComplaintCoordinator ..> ComplaintResolutionPolicy : validate decision / timeout outcome',
          'EvidenceDeadlineTimer ..> ComplaintCoordinator : evidence deadline reached',
          'ComplaintCoordinator ..> DisputeTicket : inspect / resolve',
          'ComplaintCoordinator ..> EvidenceRequest : request / check response',
          'ComplaintCoordinator ..> Order : related order / refund state',
          'ComplaintCoordinator ..> RefundRequest : manual request for Finance',
          'ComplaintCoordinator ..> FarmerPayout : release unsettled payment on rejection',
          'ComplaintCoordinator ..> Notification : notify involved parties',
          'ComplaintCoordinator ..> AuditLog : record decision / Evidence Overdue',
          'Order "1" -- "0..*" DisputeTicket : related complaints',
          'DisputeTicket "1" *-- "0..*" ComplaintEvidence : submitted evidence',
          'DisputeTicket "1" *-- "0..*" EvidenceRequest : additional information',
          'DisputeTicket "1" -- "0..1" RefundRequest : approved refund request',
          'Order "1" -- "0..*" FarmerPayout : seller payouts',
      ], '''Resolution = FullRefund | PartialRefund | RejectClaim.
Partial: 0 < refundAmount <= Order.totalAmount; Full: totalAmount.
Refund decision: complaint Resolved-RefundPending; order RefundPending.
Finance executes manual transfer; only completion makes order Refunded.
Reject: complaint Rejected; order stays Delivered; release unsettled payout.
Evidence deadline = 48h; no automatic restock or reputation change.''', '''
actor "Dispute Officer" as A
participant "«user interaction»\n: ComplaintResolutionUI" as UI
participant "«coordinator»\n: ComplaintCoordinator" as CO
participant "«business logic»\n: ComplaintResolutionPolicy" as P
participant "«entity»\n: DisputeTicket" as D
participant "«entity»\n: EvidenceRequest" as E
participant "«entity»\n: Order" as O
participant "«entity»\n: RefundRequest" as R
participant "«entity»\n: FarmerPayout" as F
participant "«entity»\n: AuditLog" as L
participant "«entity»\n: Notification" as N
participant "«timer»\n: EvidenceDeadlineTimer" as T
A ->> UI : Open / search / filter complaint cases
UI ->> CO : Complaint list request
CO ->> D : Matching complaints
D ->> CO : Cases
CO ->> UI : Cases / no matching complaint
UI ->> A : List or no-results message
opt [case selected]
  A ->> UI : View case
  UI ->> CO : Case details(ticketId)
  CO ->> D : Description, submitted evidence and decision history
  D ->> CO : Complaint information including evidence references
  CO ->> O : Related order information
  O ->> CO : Order amount, status and timeline
  CO ->> UI : Case and order details
  UI ->> A : Review complaint
  alt [request additional information]
    A ->> UI : Requested party and missing evidence
    UI ->> CO : Information request
    CO ->> E : Record requested party; deadline = now + 48 hours
    CO ->> D : Waiting-for-information status
    CO ->> N : Notify requested party
    note over CO,E
    At deadline: resolve against a nonresponding party;
    if both fail, reject complaint and retain Delivered.
    Log Evidence Overdue; single-party monetary outcome
    requires a policy mapping not specified in the SRS.
    end note
    opt [48-hour evidence deadline reached]
      T ->> CO : Evidence deadline(ticketId)
      CO ->> E : Requested parties and response timestamps
      E ->> CO : Evidence response state
      alt [both parties fail to respond]
        CO ->> D : Automatically reject complaint
        CO ->> O : Retain Delivered
        CO ->> F : Release any unsettled seller payment
        CO ->> L : Evidence Overdue and rejection outcome
        CO ->> N : Notify involved parties
      else [one requested party fails to respond]
        CO ->> P : Resolve against nonresponding party
        P ->> CO : Resolution determined by applicable evidence policy
        CO ->> L : Evidence Overdue
        alt [policy outcome is Full Refund / Partial Refund]
          CO ->> R : Manual refund request for Finance
          CO ->> D : Resolved-RefundPending with decision and reason
          CO ->> O : RefundPending
        else [policy outcome is Reject Claim]
          CO ->> D : Rejected with decision and reason
          CO ->> O : Retain Delivered
          CO ->> F : Release unsettled seller payment
        end
        CO ->> L : Automatic evidence-timeout decision
        CO ->> N : Notify involved parties
        note over CO,P : SRS does not define the single-party decision/amount mapping.
      else [all requested evidence submitted]
        CO ->> UI : Continue evidence review
      end
    end
  else [cancel resolution]
    A ->> UI : Cancel
    UI ->> A : Unchanged complaint details
  else [confirm resolution]
    A ->> UI : Resolution, reason, fault party, refund amount if partial
    UI ->> CO : Confirmed decision
    CO ->> P : Required fields and resolution/amount validity
    P ->> CO : Validation result
    alt [missing or invalid information]
      CO ->> UI : Field errors; no changes
      UI ->> A : Correct and resubmit
    else [valid]
      alt [Full Refund / Partial Refund]
        CO ->> R : Create manual refund request for Finance
        CO ->> D : Record decision; Resolved-RefundPending
        CO ->> O : Status = RefundPending
      else [Reject Claim]
        CO ->> D : Record decision; status = Rejected
        CO ->> O : Retain Delivered
        CO ->> F : Release any unsettled seller payment
      end
      CO ->> L : Actor, decision, reason, fault party and time
      CO ->> N : Notify involved parties of outcome
      CO ->> UI : Confirmed case outcome
      UI ->> A : Display latest status
    end
  end
end
note over R,F
Following Finance workflow (outside this UC):
successful manual transfer -> Refund Completed + Order Refunded;
failed transfer -> remain pending.
No automatic stock or ReputationScore update.
end note
''', 'NF1-8; all alternative flows; expanded postconditions of II.4.4; III.4.3.1.',
      'Actor lấy từ Primary Actors: Dispute Officer; các bước vẫn dùng chữ Administrator là bất nhất của nguồn. EvidenceRequest và RefundRequest là lớp suy ra từ hậu điều kiện. EvidenceDeadlineTimer là đề xuất lớp timer để kích hoạt tự xử lý hết 48h; SRS chưa định nghĩa job cụ thể hay mapping outcome/số tiền khi một bên không phản hồi. Không giả định officer thực hiện chuyển tiền.')

model('UC-PS-01', 'UC-ADM-07', 'Manage Promotions and Vouchers', 'II.4.5', 54,
      'Marketing / Promotion Staff', 'PromotionManagementUI', 'PromotionCoordinator', [
          C('Promotion', 'entity', 'promotionId: Identifier', 'name: String', 'discountRule: DiscountRule', 'eligibility: EligibilityRule', 'usageLimit: Integer', 'startAt: DateTime', 'endAt: DateTime', 'isActive: Boolean'),
          C('Voucher', 'entity', 'code: String', 'discountType: DiscountType', 'discountValue: Decimal', 'maxDiscountAmount: Money [0..1]', 'minOrderAmount: Money', 'usageLimit: Integer', 'usedCount: Integer', 'startAt: DateTime', 'endAt: DateTime', 'isActive: Boolean'),
          C('PromotionPolicy', 'business logic', '{validate discount, eligibility, period and quota}', '{decide remove or archive under applicable rule}'), AUDIT,
      ], [
          'PromotionManagementUI ..> PromotionCoordinator : management requests',
          'PromotionCoordinator ..> PromotionPolicy : validate / removal decision',
          'PromotionCoordinator ..> Promotion : query / maintain',
          'PromotionCoordinator ..> Voucher : query / maintain',
          'PromotionCoordinator ..> AuditLog : record confirmed action',
      ], '''VoucherCode globally unique (I.3.2).
Discount and period must be valid; enforce eligibility and quota.
Active vouchers apply only within validity and remaining usage limit.
No Promotion-Voucher ownership cardinality is specified in Report 3.
Delete vs archive rule is referenced but not fully defined.''', '''
actor "Marketing / Promotion Staff" as A
participant "«user interaction»\n: PromotionManagementUI" as UI
participant "«coordinator»\n: PromotionCoordinator" as CO
participant "«business logic»\n: PromotionPolicy" as P
participant "«entity»\npromotions: Promotion [*]" as PR
participant "«entity»\nvouchers: Voucher [*]" as V
participant "«entity»\n: AuditLog" as L
A ->> UI : Open / search / filter promotions and vouchers
UI ->> CO : Query(name/code, type, status)
CO ->> PR : Matching promotions
PR ->> CO : Promotion records
CO ->> V : Matching vouchers
V ->> CO : Voucher records
CO ->> UI : Combined list and validity dates
UI ->> A : List / no matching item
alt [Create / Edit]
  A ->> UI : Select New Promotion/Voucher or Edit
  UI ->> CO : Detail request(itemType, itemId)
  alt [Promotion]
    CO ->> PR : Current details / defaults
    PR ->> CO : Promotion values
  else [Voucher]
    CO ->> V : Current details / defaults
    V ->> CO : Voucher values
  end
  CO ->> UI : Form values
  UI ->> A : Discount, eligibility, usage limit, validity
  alt [Cancel changes]
    A ->> UI : Cancel
    UI ->> A : List; unsaved values discarded
  else [Save]
    A ->> UI : Submit values
    UI ->> CO : Save request
    CO ->> P : Validate required fields, discount, dates, quota and unique voucher code
    P ->> CO : Validation result
    alt [invalid]
      CO ->> UI : Field errors
      UI ->> A : Correct and resubmit
    else [valid]
      alt [Promotion]
        CO ->> PR : Create / update promotion
      else [Voucher]
        CO ->> V : Create / update voucher
      end
      CO ->> L : Actor and saved changes
      CO ->> UI : Refreshed list
      UI ->> A : Save confirmation
    end
  end
else [Activate / Deactivate]
  A ->> UI : Change active status
  UI ->> A : Status-change confirmation
  opt [confirmed]
    A ->> UI : Confirm
    UI ->> CO : Confirmed status change
    CO ->> P : Validate status-change eligibility
    P ->> CO : Eligibility result
    opt [eligible]
      alt [Promotion]
        CO ->> PR : Update active status
      else [Voucher]
        CO ->> V : Update active status
      end
      CO ->> L : Confirmed status change
      CO ->> UI : Updated list and confirmation
    end
  end
else [Delete]
  A ->> UI : Delete item
  UI ->> A : Deletion confirmation
  opt [confirmed]
    A ->> UI : Confirm Delete
    UI ->> CO : Confirmed deletion
    CO ->> P : Applicable remove/archive rule
    P ->> CO : Remove / archive / unresolved policy
    alt [rule permits removal or archive]
      alt [Promotion]
        CO ->> PR : Apply permitted remove/archive action
      else [Voucher]
        CO ->> V : Apply permitted remove/archive action
      end
      CO ->> L : Removal / archival action
      CO ->> UI : Updated list and confirmation
    else [applicable rule not defined]
      CO ->> UI : Preserve item; policy clarification needed
    end
  end
end
''', 'NF1-8; delete/invalid/cancel/no-results alternatives; III.4.4.1; I.3.2 Voucher; GBR-17.',
      'Promotion là khái niệm trực tiếp có trong UC nhưng chưa mô tả riêng ở danh sách entity. Không tự tạo composition Promotion-Voucher hay quan hệ với Product vì đặc tả chưa xác định. Nhánh giữ nguyên khi chưa có rule xóa là đề xuất xử lý phần SRS còn thiếu, không phải yêu cầu đã chốt.')

model('UC-ADM-01', 'UC-ADM-01', 'View Platform Dashboard and Pending Work', 'II.4.6', 55,
      'System Administrator', 'PlatformDashboardUI', 'DashboardCoordinator', [
          C('DashboardQuery', 'service', '{read platform metrics and available pending work}'),
          C('DashboardSummary', 'value object', 'gmv: Money {derived}', 'totalOrders: Integer {derived}', 'deliverySuccessRate: Decimal {derived}', 'cancellationRate: Decimal {derived}', 'consumerGrowth: Integer {derived}', 'farmerGrowth: Integer {derived}', 'buyerGrowth: Integer {derived}', 'totalPendingWork: Integer {derived}'),
          C('PendingWorkItem', 'value object', 'taskType: TaskType', 'referenceId: Identifier', 'submittedBy: String', 'submittedAt: DateTime', 'waitTime: Duration {derived}', 'status: String', 'action: NavigationTarget'),
          C('Order', 'entity', 'orderCode: String', 'totalAmount: Money', 'status: OrderStatus'),
          USER,
          C('KycApplication', 'entity', 'applicationId: Identifier', 'submittedAt: DateTime', 'status: KycStatus'),
          C('Product', 'entity', 'productId: Identifier', 'submittedAt: DateTime', 'status: ListingStatus'),
          C('DisputeTicket', 'entity', 'ticketId: Identifier', 'submittedAt: DateTime', 'status: ComplaintStatus'),
      ], [
          'PlatformDashboardUI ..> DashboardCoordinator : query / refresh / navigation',
          'DashboardCoordinator ..> DashboardQuery : authorized dashboard query',
          'DashboardQuery ..> Order : GMV and order counts',
          'DashboardQuery ..> User : growth by role',
          'DashboardQuery ..> KycApplication : pending KYC',
          'DashboardQuery ..> Product : pending listing moderation',
          'DashboardQuery ..> DisputeTicket : pending complaints',
          'DashboardQuery ..> DashboardSummary : derive response',
          'DashboardQuery ..> PendingWorkItem : project available work',
      ], '''Read-only; manual Refresh or auto-refresh every 60 seconds.
GMV excludes cancelled orders and completed refunds.
WaitTime > 24 hours -> red SLA warning.
Pending KYC -> UC-ADM-02; product -> ADM-04; complaint -> ADM-06.
Value objects are transient views, not persistent dashboard tables.
KYC heading elsewhere is UC-VO-01: source ID conflict remains.''', '''
actor "System Administrator" as A
participant "«user interaction»\n: PlatformDashboardUI" as UI
participant "«coordinator»\n: DashboardCoordinator" as CO
participant "«service»\n: DashboardQuery" as Q
participant "«entity»\norders: Order [*]" as O
participant "«entity»\nusers: User [*]" as U
participant "«entity»\napplications: KycApplication [*]" as K
participant "«entity»\nproducts: Product [*]" as PR
participant "«entity»\ncases: DisputeTicket [*]" as D
A ->> UI : Open platform dashboard
loop [initial load / filter change / manual Refresh / every 60 seconds while open]
  UI ->> CO : Dashboard request(dateRange, authorized actor)
  CO ->> Q : Summary and available pending work
  Q ->> O : Order totals and statuses for period
  O ->> Q : Order information / unavailable
  Q ->> U : User growth by Consumer / Farmer / B2B Buyer
  U ->> Q : Growth information / unavailable
  Q ->> K : Pending KYC and submission information
  K ->> Q : KYC items / unavailable
  Q ->> PR : Pending listing information
  PR ->> Q : Listing items / unavailable
  Q ->> D : Pending complaint information
  D ->> Q : Complaint items / unavailable
  Q ->> Q : Derive GMV excluding cancellations/completed refunds; order rates
  Q ->> Q : Project pending work; calculate WaitTime and SLA flag
  Q ->> CO : DashboardSummary, PendingWorkItems, data-availability indicators
  alt [some data temporarily unavailable]
    CO ->> UI : Available results and section-specific errors
    UI ->> A : Partial dashboard; unavailable sections marked
  else [no data matches filter]
    CO ->> UI : Empty-filter result
    UI ->> A : No matching data message
  else [no pending work]
    CO ->> UI : Metrics and empty pending-work list
    UI ->> A : Metrics; no pending work
  else [data available]
    CO ->> UI : Summary cards and pending-work list
    UI ->> A : Metrics; red warning when WaitTime > 24h
  end
end
opt [summary or pending-work item selected]
  A ->> UI : Select detail / work item
  UI ->> CO : Navigation request(type, referenceId)
  CO ->> UI : Relevant detail or management destination
  UI ->> A : Open KYC / product moderation / complaint details
end
note over Q,D
Read requests only; no status changes or dashboard write-back.
KYC destination: ADM-02 in dashboard postconditions,
but VO-01 in the KYC specification heading.
end note
''', 'NF1-5; all alternatives; expanded postconditions II.4.6; III.4.5.1.',
      'DashboardSummary/PendingWorkItem là value object do người mô hình hóa bổ sung, không phải stereotype riêng của taxonomy Gomaa; không ánh xạ thành bảng. DashboardQuery là service theo §8.8.3. KycApplication là lớp suy ra từ dữ liệu pending KYC. Công thức tỷ lệ và GMV khi partial refund chưa đủ chi tiết; chưa tự đặt mẫu số hoặc cách khấu trừ.')

model('UC-ADM-09', 'UC-ADM-09', 'Manage Subscription Tiers', 'II.4.7', 56,
      'System Administrator', 'SubscriptionTierUI', 'SubscriptionTierCoordinator', [
          C('B2BSubscriptionPackage', 'entity', 'packageId: Identifier', 'name: String', 'description: String', 'price: Money', 'durationDays: Integer', 'benefits: BenefitSet', 'usageLimits: UsageLimits', 'unlocksFarmerDirectContact: Boolean', 'canDownloadContractTemplate: Boolean', 'isActive: Boolean'),
          C('B2BSubscription', 'entity', 'subscriptionId: Identifier', 'startAt: DateTime', 'endAt: DateTime', 'status: SubscriptionStatus'),
          C('SubscriptionTierPolicy', 'business logic', '{validate name, price, period, benefits and limits}', '{decide remove/archive under applicable rule}'), AUDIT,
      ], [
          'SubscriptionTierUI ..> SubscriptionTierCoordinator : tier requests',
          'SubscriptionTierCoordinator ..> SubscriptionTierPolicy : validate / removal rule',
          'SubscriptionTierCoordinator ..> B2BSubscriptionPackage : query / maintain',
          'SubscriptionTierPolicy ..> B2BSubscription : referenced subscriptions',
          'SubscriptionTierPolicy ..> B2BSubscriptionPackage : check unique name',
          'SubscriptionTierCoordinator ..> AuditLog : record confirmed action',
          'B2BSubscriptionPackage "1" -- "0..*" B2BSubscription : purchased package',
      ], '''PackageName globally unique (I.3.2).
Validate required tier fields before saving.
Deletion/removal must follow applicable business rule.
Effects of package edits on existing subscriptions are unspecified.
No subscription payment, purchase or renewal is performed by this UC.''', '''
actor "System Administrator" as A
participant "«user interaction»\n: SubscriptionTierUI" as UI
participant "«coordinator»\n: SubscriptionTierCoordinator" as CO
participant "«business logic»\n: SubscriptionTierPolicy" as P
participant "«entity»\npackages: B2BSubscriptionPackage [*]" as T
participant "«entity»\nsubscriptions: B2BSubscription [*]" as S
participant "«entity»\n: AuditLog" as L
A ->> UI : Open / search / filter subscription tiers
UI ->> CO : Tier query(name, status)
CO ->> T : Matching tiers
T ->> CO : Tier records
CO ->> UI : Tier list
UI ->> A : List / no matching tier
alt [New Tier / Edit]
  A ->> UI : Select new or existing tier
  UI ->> CO : Tier detail request
  CO ->> T : Details / create defaults
  T ->> CO : Tier values
  CO ->> UI : Tier form
  UI ->> A : Name, price, billing period, benefits, limits, status
  alt [Cancel]
    A ->> UI : Cancel changes
    UI ->> A : Discard unsaved values; return to list
  else [Save]
    A ->> UI : Submit tier details
    UI ->> CO : Save request
    CO ->> P : Validate fields and unique package name
    P ->> T : Existing package names
    T ->> P : Matching names
    P ->> CO : Validation result
    alt [invalid or incomplete]
      CO ->> UI : Field errors
      UI ->> A : Correct and resubmit
    else [valid]
      CO ->> T : Create / update tier
      CO ->> L : Actor and tier changes
      CO ->> UI : Updated list
      UI ->> A : Save confirmation
    end
  end
else [Activate / Deactivate]
  A ->> UI : Change active status
  UI ->> A : Confirmation
  opt [confirmed]
    A ->> UI : Confirm
    UI ->> CO : Confirmed status change
    CO ->> T : Update availability for purchase
    CO ->> L : Confirmed tier status change
    CO ->> UI : Updated list and confirmation
  end
else [Delete]
  A ->> UI : Delete tier
  UI ->> A : Request confirmation
  opt [confirmed]
    A ->> UI : Confirm Delete
    UI ->> CO : Confirmed tier deletion
    CO ->> P : Applicable remove/archive rule
    P ->> S : References to selected tier
    S ->> P : Existing subscriptions
    P ->> CO : Permitted action / unresolved rule
    alt [applicable rule permits remove or archive]
      CO ->> T : Apply permitted remove/archive action
      CO ->> L : Removal / archival decision
      CO ->> UI : Updated list and confirmation
    else [rule unresolved]
      CO ->> UI : Preserve tier and existing subscriptions; clarify rule
    end
  end
end
''', 'NF1-8; invalid/cancel/delete/no-results alternatives; III.4.6.1; I.3.2 subscription entities.',
      'Benefits và UsageLimits là kiểu giá trị, chưa tự tách bảng entitlement. Billing period được biểu diễn bằng DurationDays theo I.3.2; tháng/năm cần thống nhất thêm. Chặn thay đổi khi chưa có rule xử lý lịch sử là đề xuất bảo toàn dữ liệu; không tự quyết định gói có người dùng thì cấm xóa hoặc tự migrate quyền lợi.')

def build_class(m):
    declarations = [C(m['ui'], 'user interaction', '{receive input and display results}'), C(m['ctl'], 'coordinator', '{coordinate this use case}')]+m['classes']
    lines = ['@startuml', 'allowmixing', '!pragma layout smetana', COMMON, 'hide circle', 'hide empty methods', 'top to bottom direction', 'skinparam nodesep 40', 'skinparam ranksep 55',
             f'title\n{m["code"]} - {m["title"]}\nAnalysis Class Diagram | Report 3: {m["source"]}\nend title',
             f'actor "{m["actor"]}" as Actor', f'Actor -- {m["ui"]} : interacts with']
    for name, stereotype, attrs in declarations:
        lines += [f'class {name} <<{stereotype}>> {{'] + ['  '+a for a in attrs] + ['}']
    # Dependency semantics are carried by dashed arrows; omit verbose labels
    # so the structural associations and endpoint multiplicities remain legible.
    lines += [r.split(' : ', 1)[0] if '..>' in r else r for r in m['relations']]
    lines += ['note as Rules', m['constraints'], 'end note', f'{m["ctl"]} .. Rules',
              'legend bottom left', 'Solid line: structural association; end labels: multiplicity',
              'Filled diamond: whole-part composition | Dashed arrow: usage dependency',
              'Boundary/control dependencies are not database foreign keys.', 'endlegend',
              f'footer Gomaa Ch.7-9 | Analysis realization | Source: Report 3 {m["section"]}', '@enduml']
    return '\n'.join(lines)+'\n'

def build_sequence(m):
    seq = re.sub(r'"([^"]*)"', lambda hit: hit.group(0).replace('\n', r'\n'), m['seq'])
    # PlantUML adds guard brackets itself; explicit [words] are parsed as links.
    seq = re.sub(r'^(\s*(?:alt|else|opt|loop)) \[([^\n]*)\]$', r'\1 \2', seq, flags=re.MULTILINE)
    return ('@startuml\n'+COMMON+'hide footbox\n'
            'skinparam sequenceMessageAlign left\nskinparam maxMessageSize 230\n'
            'skinparam ParticipantPadding 18\nskinparam BoxPadding 8\n'
            'skinparam sequenceParticipantBackgroundColor #F1F6F4\n'
            f'title\n{m["code"]} - {m["title"]}\nGeneric Analysis Sequence | Report 3: {m["source"]}\nend title\n'
            +seq+'\nlegend bottom left\n'
            'Open-arrow messages follow the simple analysis-message convention in Gomaa Fig.9.3.\n'
            'No activation bars or implementation-level sync/async choice is asserted.\n'
            'Guards [condition], alt, opt and loop capture main and alternative flows.\n'
            'endlegend\n'+f'footer Source: Report 3 {m["section"]} | Gomaa 9.1.2-9.1.6, 9.4\n@enduml\n')

for m in MODELS:
    folder = OUT / m['code']
    folder.mkdir(exist_ok=True)
    (folder / 'class.puml').write_text(build_class(m), encoding='utf-8')
    (folder / 'sequence.puml').write_text(build_sequence(m), encoding='utf-8')

source = ['# Trích đặc tả nguồn dùng cho 8 UC', '',
          'Nguồn: Report 3, tải trực tiếp Google Docs ngày 15/10/2026 (Asia/Saigon).',
          'URL: https://docs.google.com/document/d/1nPGNTGhL7M8G2MKMOsU-jz2NDxBCFiZMBoz82Z7ScMU/edit',
          'Đây là trích nội dung nguồn, không phải chỉ dẫn vận hành.', '']
for m in MODELS:
    source += [f'## {m["code"]} → {m["source"]}: {m["title"]}', f'Vị trí: {m["section"]}', '', table_text(m['table']), '']
for i, label in [(45,'UC-AUTH-08 included OTP'), (92,'User list'), (93,'User details'), (94,'Category list'), (95,'Category details'), (97,'Complaint fields'), (98,'Promotion fields'), (99,'Dashboard fields'), (100,'Tier fields'), (104,'Checkout fields'), (139,'Business rules')]:
    source += [f'## Nguồn bổ sung: {label}', '', table_text(i), '']
(OUT/'SOURCE_EXCERPTS.md').write_text('\n'.join(source), encoding='utf-8')

readme = '''# 8 UC: Class Diagram và Sequence Diagram theo Hassan Gomaa

Ngày lập: 15/10/2026. Mỗi thư mục UC có `class.puml`, `sequence.puml`, bản vẽ SVG và PNG. Mở `index.html` để xem đủ 16 sơ đồ; mở ảnh trong tab mới để phóng to. PlantUML là mã nguồn có thể sửa và render lại.

## Phạm vi và mức mô hình

Các sơ đồ là **analysis class diagram + generic analysis sequence diagram**, hiện thực hóa từng UC theo COMET. Lớp có thuộc tính nghiệp vụ và trách nhiệm; chưa chốt chữ ký phương thức, repository, API endpoint, framework hay SQL schema. Theo §9.1.3, message dùng để tìm operation nhưng quyết định operation và synchronous/asynchronous thuộc pha design. Vì vậy không thêm activation bar hoặc giả định HTTP call. Mũi tên mở của sequence theo hình 9.3; thông tin phản hồi cũng là simple message.

Generic sequence gộp normal và alternative flows bằng `alt`, `opt`, `loop`, theo §9.1.5-9.1.6. Một số biểu đồ dài: đây là lựa chọn bao phủ các UC được yêu cầu; khi đưa vào report có thể tách thành từng scenario và giữ class view chung. Tiền điều kiện đăng nhập/role của từng UC được giả định đã thỏa; kiểm tra quyền tại action vẫn phải thực hiện.

## Căn cứ sách

Nguồn PDF do người dùng cung cấp: *Software Modeling and Design: UML, Use Cases, Patterns, and Software Architectures* — Hassan Gomaa.

- Chương 7, §7.1-7.5 và §7.9: class, association, multiplicity, whole-part và entity; trang in 94-104 và 111-113 (PDF 122-132 và 139-141).
- §8.2-8.3: phân loại boundary/entity/control/application logic; trang in 116-118 (PDF 144-146), hình 8.1.
- §8.5.1-8.5.2: user interaction và proxy; trang in 119-120 (PDF 147-148).
- §8.7.1 và §8.8: coordinator và business logic/service; trang in 125 và 128-130 (PDF 153,156-158).
- §9.1.2-9.1.6: sequence, simple analysis message, scenario/generic form; trang in 134-136 (PDF 162-164), hình 9.3.
- §9.4: từ external input → boundary → internal participants → message sequence → alternatives; trang in 139-140 (PDF 167-168).

Tên `«value object»` dùng trong dashboard là chú thích mô hình bổ sung của người lập, không được trình bày như một loại riêng trong taxonomy Gomaa. Entity và class không đồng nghĩa với bảng database. Dependency giữa UI/coordinator/policy không mang multiplicity; association nghiệp vụ mới dùng multiplicity. Composition chỉ dùng với phần có vòng đời phụ thuộc rõ (CartItem, OrderItem, evidence/request thuộc case).

## Ánh xạ ảnh yêu cầu sang Report 3

| Mã trong ảnh / thư mục | Mã trong đặc tả Report 3 | UC | Vị trí |
|---|---|---|---|
'''
for m in MODELS:
    readme += f'| {m["code"]} | {m["source"]} | {m["title"]} | {m["section"]} |\n'
readme += '''
## Lưu ý về nguồn và quyết định mô hình

Các tên UI/coordinator/policy do người mô hình hóa đặt theo vai trò Gomaa; Report 3 không cung cấp sẵn thiết kế class. Không tuyên bố đây là cách chia lớp duy nhất. Thuộc tính được chọn để thể hiện phạm vi UC, không phải danh sách đầy đủ của cả hệ thống. Cùng tên class ở nhiều UC là cùng một khái niệm dùng lại, mỗi view chỉ hiển thị phần liên quan.

Report 3 còn một số điểm chưa thống nhất: mã checkout trong ảnh là RC-02 nhưng heading là CON-02; complaint CS-01 tương ứng ADM-06; promotion PS-01 tương ứng ADM-07. Dashboard gọi KYC ADM-02 trong hậu điều kiện, nhưng heading KYC là VO-01. Payment được gọi là RC-03 ở heading và CON-03 ở checkout field table. Tài liệu giữ dấu vết các mã này, không sửa nguồn Google Docs.

Các điểm chưa có quy tắc đủ chi tiết không được tự chốt: xóa/archival promotion và subscription tier; tác động sửa tier đến subscription đã mua; xác định outcome/amount khi một bên không nộp chứng cứ; GMV khi partial refund; công thức mẫu số các tỷ lệ dashboard; giữ/trừ điểm và quota voucher; phản hồi email không tồn tại trong reset. Chi tiết từng UC ở dưới.

'''
for m in MODELS:
    readme += f'### {m["code"]} — {m["title"]}\n\nActor: {m["actor"]}.\n\nĐối chiếu: {m["trace"]}\n\n{m["decisions"]}\n\n'
readme += '''## Kiểm tra và sử dụng

Các file PlantUML được render tại máy bằng PlantUML 1.2025.4, không gửi nội dung đặc tả tới dịch vụ render online. SVG phù hợp khi cần phóng to; PNG để chèn report; PUML để sửa nội dung. `SOURCE_EXCERPTS.md` lưu các bảng đặc tả và quy tắc đã đối chiếu để truy vết. Các ghi chú giới hạn đặc tả là một phần của kết quả, cần giữ khi duyệt thiết kế.

Render lại từ workspace:

```powershell
java -DPLANTUML_LIMIT_SIZE=16000 -jar tmp/plantuml-1.2025.4.jar -charset UTF-8 -tsvg -r "output/UC_UML_Gomaa/**.puml"
java -DPLANTUML_LIMIT_SIZE=16000 -jar tmp/plantuml-1.2025.4.jar -charset UTF-8 -tpng -r "output/UC_UML_Gomaa/**.puml"
```
'''
(OUT/'README.md').write_text(readme, encoding='utf-8')

cards=[]
nav=[]
for m in MODELS:
    nav.append(f'<a href="#{m["code"]}">{m["code"]}</a>')
    diagrams=''.join(f'<article><h3>{label}</h3><p><a href="{m["code"]}/{kind}.svg" target="_blank">Mở SVG / phóng to</a> · <a href="{m["code"]}/{kind}.png">PNG</a> · <a href="{m["code"]}/{kind}.puml">Mã PlantUML</a></p><div class="canvas"><img src="{m["code"]}/{kind}.svg" alt="{kind} diagram {m["code"]}" loading="lazy"></div></article>' for kind,label in [('class','Class diagram'),('sequence','Sequence diagram')])
    cards.append(f'<section id="{m["code"]}"><span class="code">{m["code"]} → Report 3: {m["source"]}</span><h2>{escape(m["title"])}</h2><p>{escape(m["actor"])} · {m["section"]}</p><details><summary>Truy vết và quyết định mô hình</summary><p>{escape(m["trace"])}</p><p>{escape(m["decisions"])}</p><pre>{escape(m["constraints"])}</pre></details>{diagrams}</section>')
html='''<!doctype html><html lang="vi"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>AgriSustain — 8 UC UML theo Gomaa</title>
<style>body{margin:0;background:#f3f5f3;color:#18352c;font:16px/1.6 Arial,sans-serif}header,main{max-width:1480px;margin:auto;padding:32px}header{padding-bottom:8px}h1{font-size:36px;line-height:1.2}h2{margin:6px 0}h3{margin-bottom:0}a{color:#17624c}nav{display:flex;gap:10px;flex-wrap:wrap}nav a,.code{padding:6px 12px;background:#e3eee7;border-radius:5px;font-weight:bold}section{background:white;border:1px solid #d8e2dc;border-radius:10px;padding:25px;margin:24px 0}article{border-top:1px solid #d8e2dc;margin-top:24px;padding-top:10px}.canvas{overflow:auto;border:1px solid #e3e8e4;padding:18px;background:white}.canvas img{display:block;max-width:none;width:auto;height:auto}details{background:#fffbed;padding:12px 16px;border-radius:5px}summary{cursor:pointer;font-weight:bold}pre{white-space:pre-wrap;font:14px/1.6 Arial}footer{padding:30px;text-align:center;color:#52675b}@media print{nav,details,article>p{display:none}.canvas{overflow:visible;border:0}.canvas img{max-width:100%;height:auto}article{break-before:page}section{border:0;padding:0}header{break-after:page}}</style>
<header><p>AGRISUSTAIN · REPORT 3 · 15/10/2026</p><h1>8 Use Cases<br>Class & Sequence Diagrams</h1><p>Mô hình phân tích theo Hassan Gomaa / COMET. Mỗi UC có một class diagram và một generic sequence diagram bao phủ normal và alternative flows.</p><p><a href="README.md">Hướng dẫn & căn cứ sách</a> · <a href="SOURCE_EXCERPTS.md">Trích đặc tả nguồn</a></p><nav>'''+''.join(nav)+'</nav></header><main>'+''.join(cards)+'</main><footer>16 diagrams · Local rendering · Editable PlantUML sources · Source discrepancies documented</footer></html>'
html = html.replace('max-width:none', 'max-width:100%')
(OUT/'index.html').write_text(html, encoding='utf-8')
(OUT/'manifest.json').write_text(json.dumps([{k:m[k] for k in ['code','source','title','section','trace']} for m in MODELS], ensure_ascii=False, indent=2), encoding='utf-8')
print(f'Created {len(MODELS)*2} PlantUML sources in {OUT}')
