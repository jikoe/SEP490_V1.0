const fs = require('fs');
const path = require('path');

const out = path.resolve(__dirname, '..', 'output', 'Project_Tracking_Feature_Class_Diagrams.drawio');
const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const label = (name, attributes = []) => {
  const attrs = attributes.map(esc).join('<br>');
  return `<div style="font-size:14px;font-weight:bold;text-align:center">${esc(name)}</div><hr><div style="font-size:11px;text-align:left">${attrs}</div>`;
};
const classStyle = (tone = 'blue') => {
  const fills = { blue: '#dae8fc', green: '#d5e8d4', orange: '#ffe6cc', purple: '#e1d5e7', gray: '#f5f5f5' };
  return `html=1;whiteSpace=wrap;rounded=0;fillColor=${fills[tone] || fills.blue};strokeColor=#4d4d4d;align=left;verticalAlign=top;spacing=8;fontFamily=Helvetica;`;
};
const noteStyle = 'shape=note;whiteSpace=wrap;html=1;backgroundOutline=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=11;align=left;verticalAlign=top;spacing=7;';

const features = [
  {
    name: '00 Feature Review & Assumptions',
    note: '<b>Tracking review</b><br><br><b>Reasonable coverage:</b> 42 UCs cover onboarding, catalogue, retail, farm operations, traceability, B2B and governance.<br><br><b>Resolve before build:</b><br>1. GBR-01 says one role/account, while role splitting may imply many. These diagrams assume <b>one active primary role</b>.<br>2. Checkout validates inventory but does not specify reservation/expiry; overselling is possible.<br>3. Finance Staff has responsibilities, but no tracked finance/reconciliation/payout UC.<br>4. Complaint decision, refund and payment reconciliation lack an end-to-end state model.<br>5. B2B contract, order and milestone ownership/linkage are incomplete.<br>6. Certificate approval/revocation and promotion reporting need explicit lifecycle rules.<br><br><b>Diagram convention:</b> each subsequent page models the data needed by one WBS Feature. Attributes are essential only; no design-level operations are asserted.',
    classes: [], edges: []
  },
  {
    name: '01 Authentication',
    note: '<b>UCs:</b> AUTH-01, 02, 03, 04, 05, 06, 07.<br><br>Assumption: an account has exactly one current primary Role; exactly one corresponding business profile is present. If multiple roles are allowed, replace User.roleId with a UserRole assignment model.',
    classes: [
      ['User', ['id: UUID', 'email: String {unique}', 'passwordHash: String', 'status: AccountStatus', 'emailVerified: Boolean'], 'blue'],
      ['Role', ['id: UUID', 'code: String {unique}', 'name: String'], 'purple'],
      ['ConsumerProfile', ['userId: UUID', 'fullName: String', 'phone: String'], 'green'],
      ['FarmerProfile', ['userId: UUID', 'farmName: String', 'kycStatus: KycStatus'], 'green'],
      ['BuyerProfile', ['userId: UUID', 'companyName: String', 'taxCode: String'], 'green'],
      ['PasswordResetToken', ['id: UUID', 'tokenHash: String', 'expiresAt: DateTime', 'usedAt: DateTime?'], 'orange'],
      ['AuthSession', ['id: UUID', 'issuedAt: DateTime', 'expiresAt: DateTime', 'revokedAt: DateTime?'], 'orange'],
      ['AccountStatusHistory', ['id: UUID', 'fromStatus: AccountStatus', 'toStatus: AccountStatus', 'reason: String', 'changedAt: DateTime'], 'gray']
    ],
    edges: [['User','Role','0..*','1'], ['User','ConsumerProfile','1','0..1'], ['User','FarmerProfile','1','0..1'], ['User','BuyerProfile','1','0..1'], ['User','PasswordResetToken','1','0..*'], ['User','AuthSession','1','0..*'], ['User','AccountStatusHistory','1','0..*']]
  },
  {
    name: '02 Marketplace',
    note: '<b>UCs:</b> MKT-01, MKT-02.<br><br>Only Approved and Available products should appear in browse/search. Batch information is viewable with the product but inventory must be governed by HarvestBatch, not a generic Product total.',
    classes: [
      ['ProductCategory', ['id: UUID', 'name: String', 'status: CategoryStatus'], 'orange'],
      ['Product', ['id: UUID', 'name: String', 'retailPrice: Money', 'unit: String', 'listingStatus: ListingStatus'], 'blue'],
      ['ProductImage', ['id: UUID', 'url: String', 'sortOrder: Integer'], 'gray'],
      ['HarvestBatch', ['id: UUID', 'batchCode: String', 'availableQuantity: Decimal', 'expiryDate: Date?'], 'green'],
      ['FarmerProfile', ['userId: UUID', 'farmName: String', 'kycStatus: KycStatus'], 'green'],
      ['ProductReview', ['id: UUID', 'rating: Integer', 'content: String', 'status: ReviewStatus'], 'purple']
    ],
    edges: [['ProductCategory','ProductCategory','0..1','0..*'], ['ProductCategory','Product','1','0..*'], ['FarmerProfile','Product','1','0..*'], ['Product','ProductImage','1','1..*'], ['Product','HarvestBatch','1','0..*'], ['Product','ProductReview','1','0..*']]
  },
  {
    name: '03 Product Management',
    note: '<b>UC:</b> FAR-03.<br><br>A Farmer may submit a listing only after KYC approval. The moderation decision is kept separately so status changes remain auditable.',
    classes: [
      ['FarmerProfile', ['userId: UUID', 'farmName: String', 'kycStatus: KycStatus'], 'green'],
      ['Product', ['id: UUID', 'name: String', 'origin: String', 'retailPrice: Money', 'listingStatus: ListingStatus'], 'blue'],
      ['ProductCategory', ['id: UUID', 'name: String', 'status: CategoryStatus'], 'orange'],
      ['ProductImage', ['id: UUID', 'url: String', 'sortOrder: Integer'], 'gray'],
      ['ListingModerationDecision', ['id: UUID', 'decision: ApprovalDecision', 'reason: String', 'decidedAt: DateTime'], 'purple']
    ],
    edges: [['FarmerProfile','Product','1','0..*'], ['ProductCategory','Product','1','0..*'], ['Product','ProductImage','1','1..*'], ['Product','ListingModerationDecision','1','0..*']]
  },
  {
    name: '04 Administration',
    note: '<b>UCs:</b> ADM-01, 02, 03, 04, 05, 06, 07, 08, 09.<br><br>Role ownership: System Administrator—accounts/categories/subscription tiers/dashboard; Verification Officer—KYC/certificates; Content Moderator—listings/reviews; Support Officer—complaints; Promotion Staff—promotion/vouchers. Finance remains a documented role without a tracked UC.',
    classes: [
      ['User', ['id: UUID', 'email: String', 'status: AccountStatus', 'roleId: UUID'], 'blue'],
      ['AccountStatusHistory', ['id: UUID', 'fromStatus: AccountStatus', 'toStatus: AccountStatus', 'reason: String'], 'gray'],
      ['ProductCategory', ['id: UUID', 'name: String', 'parentCategoryId: UUID?'], 'orange'],
      ['KycVerification', ['id: UUID', 'status: KycStatus', 'reviewedAt: DateTime?', 'reason: String?'], 'green'],
      ['Product', ['id: UUID', 'listingStatus: ListingStatus'], 'blue'],
      ['ProductReview', ['id: UUID', 'status: ReviewStatus'], 'blue'],
      ['Complaint', ['id: UUID', 'status: ComplaintStatus', 'resolution: String?'], 'purple'],
      ['Promotion', ['id: UUID', 'name: String', 'startAt: DateTime', 'endAt: DateTime'], 'orange'],
      ['Voucher', ['id: UUID', 'code: String {unique}', 'discountType: DiscountType', 'value: Decimal'], 'orange'],
      ['B2BSubscriptionPackage', ['id: UUID', 'name: String', 'durationDays: Integer', 'price: Money', 'status: PackageStatus'], 'green']
    ],
    edges: [['User','AccountStatusHistory','1','0..*'], ['User','KycVerification','1','0..*'], ['ProductCategory','ProductCategory','0..1','0..*'], ['Product','ProductReview','1','0..*'], ['Complaint','User','0..*','1'], ['Promotion','Voucher','1','0..*']]
  },
  {
    name: '05 Consumer Ordering',
    note: '<b>UCs:</b> CON-01, 02, 04.<br><br>Cart uses the current listing; OrderItem preserves the price and batch allocated at checkout. A stock reservation with expiry is needed if payment is asynchronous.',
    classes: [
      ['ConsumerProfile', ['userId: UUID', 'fullName: String', 'phone: String'], 'green'],
      ['DeliveryAddress', ['id: UUID', 'recipientName: String', 'phone: String', 'address: String', 'isDefault: Boolean'], 'orange'],
      ['Cart', ['id: UUID', 'createdAt: DateTime'], 'blue'],
      ['CartItem', ['id: UUID', 'quantity: Decimal'], 'blue'],
      ['Product', ['id: UUID', 'name: String', 'retailPrice: Money'], 'blue'],
      ['Order', ['id: UUID', 'orderType: OrderType', 'status: OrderStatus', 'finalAmount: Money', 'trackingNumber: String?'], 'purple'],
      ['OrderItem', ['id: UUID', 'quantity: Decimal', 'unitPrice: Money', 'allocatedBatchId: UUID'], 'purple'],
      ['VoucherRedemption', ['id: UUID', 'discountAmount: Money', 'redeemedAt: DateTime'], 'orange']
    ],
    edges: [['ConsumerProfile','DeliveryAddress','1','1..*'], ['ConsumerProfile','Cart','1','0..1'], ['Cart','CartItem','1','0..*'], ['CartItem','Product','0..*','1'], ['ConsumerProfile','Order','1','0..*'], ['Order','DeliveryAddress','0..*','1'], ['Order','OrderItem','1','1..*'], ['OrderItem','Product','0..*','1'], ['Order','VoucherRedemption','1','0..1']]
  },
  {
    name: '06 Farmer Sales',
    note: '<b>UCs:</b> FAR-05, FAR-06.<br><br>Pre-order commitments must refer to a future HarvestBatch/campaign. The tracking data does not state a reservation policy; add Reservation/expiry before accepting concurrent paid orders.',
    classes: [
      ['FarmerProfile', ['userId: UUID', 'farmName: String'], 'green'],
      ['Product', ['id: UUID', 'name: String'], 'blue'],
      ['HarvestBatch', ['id: UUID', 'availableQuantity: Decimal', 'estimatedHarvestDate: Date?'], 'green'],
      ['PreOrderCampaign', ['id: UUID', 'opensAt: DateTime', 'closesAt: DateTime', 'status: CampaignStatus'], 'orange'],
      ['Order', ['id: UUID', 'isPreOrder: Boolean', 'status: OrderStatus'], 'purple'],
      ['OrderItem', ['id: UUID', 'quantity: Decimal', 'allocatedBatchId: UUID'], 'purple'],
      ['Shipment', ['id: UUID', 'carrier: String?', 'trackingNumber: String?', 'status: ShipmentStatus'], 'gray']
    ],
    edges: [['FarmerProfile','Product','1','0..*'], ['Product','HarvestBatch','1','0..*'], ['HarvestBatch','PreOrderCampaign','1','0..*'], ['PreOrderCampaign','Order','0..1','0..*'], ['Order','OrderItem','1','1..*'], ['OrderItem','HarvestBatch','0..*','1'], ['Order','Shipment','1','0..1']]
  },
  {
    name: '07 Traceability',
    note: '<b>UC:</b> TRC-01.<br><br>Public traceability should expose only approved, non-sensitive data. A QR resolves to a concrete HarvestBatch, not merely a Product.',
    classes: [
      ['Product', ['id: UUID', 'name: String', 'origin: String'], 'blue'],
      ['HarvestBatch', ['id: UUID', 'batchCode: String', 'harvestDate: Date'], 'green'],
      ['Farm', ['id: UUID', 'name: String', 'region: String'], 'green'],
      ['CropSeason', ['id: UUID', 'cropName: String', 'startedAt: Date'], 'green'],
      ['FarmingLog', ['id: UUID', 'recordedAt: DateTime', 'activityType: String'], 'gray'],
      ['Certificate', ['id: UUID', 'certificateNo: String', 'validUntil: Date', 'status: CertificateStatus'], 'orange'],
      ['TraceabilityQRCode', ['id: UUID', 'token: String {unique}', 'status: QrStatus'], 'purple']
    ],
    edges: [['Product','HarvestBatch','1','0..*'], ['Farm','CropSeason','1','0..*'], ['CropSeason','HarvestBatch','1','0..*'], ['CropSeason','FarmingLog','1','0..*'], ['Farm','Certificate','1','0..*'], ['HarvestBatch','TraceabilityQRCode','1','1']]
  },
  {
    name: '08 Farm Management',
    note: '<b>UCs:</b> FAR-01, FAR-02.<br><br>Keep CropSeason separate from HarvestBatch: a season records growing activity, while a batch represents the saleable/traceable output.',
    classes: [
      ['FarmerProfile', ['userId: UUID', 'farmName: String'], 'green'],
      ['Farm', ['id: UUID', 'name: String', 'region: String', 'areaHectares: Decimal'], 'green'],
      ['CropSeason', ['id: UUID', 'cropName: String', 'startedAt: Date', 'expectedHarvestAt: Date'], 'green'],
      ['FarmingLog', ['id: UUID', 'recordedAt: DateTime', 'activityType: String', 'detail: String'], 'blue'],
      ['HarvestBatch', ['id: UUID', 'batchCode: String', 'harvestDate: Date', 'availableQuantity: Decimal'], 'orange']
    ],
    edges: [['FarmerProfile','Farm','1','1..*'], ['Farm','CropSeason','1','0..*'], ['CropSeason','FarmingLog','1','0..*'], ['CropSeason','HarvestBatch','1','0..*']]
  },
  {
    name: '09 Product Traceability',
    note: '<b>UC:</b> FAR-04.<br><br>Generated QR codes must be immutable references to the batch. TraceabilityEvent is proposed to preserve issuance/revocation history; its policy should be added to the SRS.',
    classes: [
      ['FarmerProfile', ['userId: UUID', 'farmName: String'], 'green'],
      ['Product', ['id: UUID', 'name: String'], 'blue'],
      ['HarvestBatch', ['id: UUID', 'batchCode: String', 'availableQuantity: Decimal'], 'green'],
      ['TraceabilityQRCode', ['id: UUID', 'token: String {unique}', 'issuedAt: DateTime', 'status: QrStatus'], 'purple'],
      ['TraceabilityEvent', ['id: UUID', 'eventType: TraceEventType', 'occurredAt: DateTime', 'details: String'], 'gray'],
      ['Certificate', ['id: UUID', 'certificateNo: String', 'validUntil: Date'], 'orange']
    ],
    edges: [['FarmerProfile','Product','1','0..*'], ['Product','HarvestBatch','1','0..*'], ['HarvestBatch','TraceabilityQRCode','1','1'], ['HarvestBatch','TraceabilityEvent','1','0..*'], ['FarmerProfile','Certificate','1','0..*']]
  },
  {
    name: '10 Consumer Payment',
    note: '<b>UC:</b> CON-03.<br><br>Payment success must be idempotent: provider webhooks can arrive more than once or late. Refund is shown because complaint resolution refers to it, but its finance approval lifecycle needs a dedicated UC.',
    classes: [
      ['Order', ['id: UUID', 'status: OrderStatus', 'finalAmount: Money'], 'purple'],
      ['PaymentTransaction', ['id: UUID', 'provider: PaymentProvider', 'amount: Money', 'status: PaymentStatus', 'providerRef: String {unique}'], 'blue'],
      ['PaymentAttempt', ['id: UUID', 'requestedAt: DateTime', 'expiresAt: DateTime?', 'status: AttemptStatus'], 'gray'],
      ['PaymentWebhookEvent', ['id: UUID', 'providerEventId: String {unique}', 'receivedAt: DateTime', 'signatureValid: Boolean'], 'orange'],
      ['Refund', ['id: UUID', 'amount: Money', 'reason: String', 'status: RefundStatus'], 'orange']
    ],
    edges: [['Order','PaymentTransaction','1','0..*'], ['PaymentTransaction','PaymentAttempt','1','1..*'], ['PaymentTransaction','PaymentWebhookEvent','1','0..*'], ['PaymentTransaction','Refund','1','0..*']]
  },
  {
    name: '11 Consumer Engagement',
    note: '<b>UCs:</b> CON-05, CON-06.<br><br>A review is permitted only for a completed OrderItem and is unique per OrderItem. Complaint evidence and the eventual resolution are separate records for auditability.',
    classes: [
      ['ConsumerProfile', ['userId: UUID', 'fullName: String'], 'green'],
      ['Order', ['id: UUID', 'status: OrderStatus'], 'purple'],
      ['OrderItem', ['id: UUID', 'quantity: Decimal'], 'purple'],
      ['ProductReview', ['id: UUID', 'rating: Integer', 'content: String', 'status: ReviewStatus'], 'blue'],
      ['ReviewModerationDecision', ['id: UUID', 'decision: ApprovalDecision', 'reason: String'], 'gray'],
      ['Complaint', ['id: UUID', 'category: String', 'status: ComplaintStatus', 'submittedAt: DateTime'], 'orange'],
      ['ComplaintEvidence', ['id: UUID', 'url: String', 'evidenceType: String'], 'gray'],
      ['ComplaintResolution', ['id: UUID', 'decision: ResolutionDecision', 'compensationAmount: Money?', 'decidedAt: DateTime'], 'orange']
    ],
    edges: [['ConsumerProfile','Order','1','0..*'], ['Order','OrderItem','1','1..*'], ['OrderItem','ProductReview','1','0..1'], ['ProductReview','ReviewModerationDecision','1','0..*'], ['Order','Complaint','1','0..*'], ['Complaint','ComplaintEvidence','1','0..*'], ['Complaint','ComplaintResolution','1','0..1']]
  },
  {
    name: '12 Certification',
    note: '<b>UC:</b> FAR-07.<br><br>The tracking UC covers dossier submission. Approval, expiry and revocation are business responsibilities of Verification Officer and need explicit status transitions in the SRS.',
    classes: [
      ['FarmerProfile', ['userId: UUID', 'farmName: String'], 'green'],
      ['CertificateDossier', ['id: UUID', 'certificateType: String', 'submittedAt: DateTime', 'status: DossierStatus'], 'blue'],
      ['CertificateDocument', ['id: UUID', 'url: String', 'documentType: String'], 'gray'],
      ['CertificateReview', ['id: UUID', 'decision: ApprovalDecision', 'reason: String', 'reviewedAt: DateTime'], 'purple'],
      ['Certificate', ['id: UUID', 'certificateNo: String', 'issuedAt: Date', 'validUntil: Date', 'status: CertificateStatus'], 'orange']
    ],
    edges: [['FarmerProfile','CertificateDossier','1','0..*'], ['CertificateDossier','CertificateDocument','1','1..*'], ['CertificateDossier','CertificateReview','1','0..*'], ['CertificateDossier','Certificate','1','0..1']]
  },
  {
    name: '13 B2B Procurement',
    note: '<b>UCs:</b> B2B-01, 02, 03, 04.<br><br>Subscription governs contact visibility/capabilities. Wholesale order allocation must follow a specific HarvestBatch and quantity rule; do not decrement generic Product stock.',
    classes: [
      ['BuyerProfile', ['userId: UUID', 'companyName: String', 'taxCode: String'], 'green'],
      ['FarmerProfile', ['userId: UUID', 'farmName: String'], 'green'],
      ['Farm', ['id: UUID', 'region: String', 'areaHectares: Decimal'], 'green'],
      ['Product', ['id: UUID', 'name: String', 'wholesalePrice: Money', 'wholesaleMinQuantity: Decimal'], 'blue'],
      ['HarvestBatch', ['id: UUID', 'availableQuantity: Decimal'], 'orange'],
      ['B2BSubscription', ['id: UUID', 'status: SubscriptionStatus', 'startedAt: Date', 'expiresAt: Date'], 'purple'],
      ['WholesaleOrder', ['id: UUID', 'status: OrderStatus', 'totalAmount: Money'], 'blue'],
      ['PreOrderCommitment', ['id: UUID', 'quantity: Decimal', 'expectedDeliveryDate: Date', 'status: CommitmentStatus'], 'orange']
    ],
    edges: [['FarmerProfile','Farm','1','1..*'], ['FarmerProfile','Product','1','0..*'], ['Product','HarvestBatch','1','0..*'], ['BuyerProfile','B2BSubscription','1','0..*'], ['BuyerProfile','WholesaleOrder','1','0..*'], ['WholesaleOrder','HarvestBatch','0..*','1'], ['BuyerProfile','PreOrderCommitment','1','0..*'], ['PreOrderCommitment','HarvestBatch','0..*','1']]
  },
  {
    name: '14 B2B Contract Management',
    note: '<b>UCs:</b> B2B-05, 06, 07, FAR-08.<br><br>Contract versioning and signatures are separated. The SRS must state whether milestones are created from the contract, an order, or both; the model shows them as contract commitments.',
    classes: [
      ['BuyerProfile', ['userId: UUID', 'companyName: String'], 'green'],
      ['FarmerProfile', ['userId: UUID', 'farmName: String'], 'green'],
      ['ContractProposal', ['id: UUID', 'status: ProposalStatus', 'submittedAt: DateTime'], 'blue'],
      ['OfftakeContract', ['id: UUID', 'contractNo: String {unique}', 'status: ContractStatus', 'effectiveDate: Date?'], 'purple'],
      ['ContractRevision', ['id: UUID', 'versionNo: Integer', 'reason: String', 'createdAt: DateTime'], 'gray'],
      ['ContractSignature', ['id: UUID', 'signedAt: DateTime', 'signerRole: SignerRole', 'signatureRef: String'], 'orange'],
      ['ContractMilestone', ['id: UUID', 'name: String', 'dueDate: Date', 'status: MilestoneStatus'], 'orange'],
      ['WholesaleOrder', ['id: UUID', 'status: OrderStatus'], 'blue']
    ],
    edges: [['BuyerProfile','ContractProposal','1','0..*'], ['FarmerProfile','ContractProposal','1','0..*'], ['ContractProposal','OfftakeContract','1','0..1'], ['OfftakeContract','ContractRevision','1','0..*'], ['OfftakeContract','ContractSignature','1','2..*'], ['OfftakeContract','ContractMilestone','1','1..*'], ['OfftakeContract','WholesaleOrder','1','0..*']]
  },
  {
    name: '15 B2B Membership',
    note: '<b>UC:</b> B2B-08.<br><br>A subscription is an individual purchase of a package. Entitlements determine the ability to see supplier contact details or create commitments; expiry must immediately revoke them.',
    classes: [
      ['BuyerProfile', ['userId: UUID', 'companyName: String'], 'green'],
      ['B2BSubscriptionPackage', ['id: UUID', 'name: String', 'durationDays: Integer', 'price: Money', 'status: PackageStatus'], 'blue'],
      ['SubscriptionEntitlement', ['id: UUID', 'code: EntitlementCode', 'isEnabled: Boolean'], 'orange'],
      ['B2BSubscription', ['id: UUID', 'status: SubscriptionStatus', 'startedAt: Date', 'expiresAt: Date'], 'purple'],
      ['PaymentTransaction', ['id: UUID', 'amount: Money', 'status: PaymentStatus', 'providerRef: String'], 'gray']
    ],
    edges: [['B2BSubscriptionPackage','SubscriptionEntitlement','1','1..*'], ['BuyerProfile','B2BSubscription','1','0..*'], ['B2BSubscriptionPackage','B2BSubscription','1','0..*'], ['B2BSubscription','PaymentTransaction','1','0..*']]
  },
  {
    name: '16 Farmer Analytics',
    note: '<b>UC:</b> FAR-09.<br><br>Revenue dashboard should be derived from finalized orders, payment transactions, settlements and commission rules. Do not present gross order value as money payable to the Farmer.',
    classes: [
      ['FarmerProfile', ['userId: UUID', 'farmName: String'], 'green'],
      ['Order', ['id: UUID', 'status: OrderStatus', 'finalAmount: Money'], 'purple'],
      ['OrderItem', ['id: UUID', 'quantity: Decimal', 'unitPrice: Money'], 'purple'],
      ['PaymentTransaction', ['id: UUID', 'amount: Money', 'status: PaymentStatus'], 'blue'],
      ['CommissionRate', ['id: UUID', 'rate: Decimal', 'effectiveFrom: Date', 'effectiveTo: Date?'], 'orange'],
      ['Settlement', ['id: UUID', 'grossAmount: Money', 'commissionAmount: Money', 'netAmount: Money', 'status: SettlementStatus'], 'orange'],
      ['RevenueSnapshot', ['id: UUID', 'periodStart: Date', 'periodEnd: Date', 'netRevenue: Money'], 'gray']
    ],
    edges: [['FarmerProfile','OrderItem','1','0..*'], ['Order','OrderItem','1','1..*'], ['Order','PaymentTransaction','1','0..*'], ['OrderItem','Settlement','1','0..*'], ['CommissionRate','Settlement','1','0..*'], ['FarmerProfile','RevenueSnapshot','1','0..*']]
  }
];

function page(feature, pageNo) {
  let cells = '<mxCell id="0"/><mxCell id="1" parent="0"/>';
  const node = new Map();
  const cols = Math.max(1, Math.min(4, Math.ceil(Math.sqrt(Math.max(1, feature.classes.length)))));
  feature.classes.forEach(([name, attrs, tone], i) => {
    const id = `p${pageNo}c${i}`;
    node.set(name, id);
    const x = 40 + (i % cols) * 290;
    const y = 65 + Math.floor(i / cols) * 190;
    cells += `<mxCell id="${id}" value="${esc(label(name, attrs))}" style="${classStyle(tone)}" vertex="1" parent="1"><mxGeometry x="${x}" y="${y}" width="245" height="140" as="geometry"/></mxCell>`;
  });
  const noteX = feature.classes.length ? 40 : 60;
  const noteY = feature.classes.length ? 65 + Math.ceil(feature.classes.length / cols) * 190 + 10 : 65;
  cells += `<mxCell id="p${pageNo}n" value="${esc(feature.note)}" style="${noteStyle}" vertex="1" parent="1"><mxGeometry x="${noteX}" y="${noteY}" width="1080" height="${feature.classes.length ? 150 : 310}" as="geometry"/></mxCell>`;
  (feature.edges || []).forEach(([source, target, sourceMult, targetMult], i) => {
    cells += `<mxCell id="p${pageNo}e${i}" value="${esc(`${sourceMult} — ${targetMult}`)}" style="edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;endArrow=none;startArrow=none;fontSize=10;labelBackgroundColor=#ffffff;" edge="1" parent="1" source="${node.get(source)}" target="${node.get(target)}"><mxGeometry relative="1" as="geometry"/></mxCell>`;
  });
  return `<diagram id="feature-${pageNo}" name="${esc(feature.name)}"><mxGraphModel dx="1200" dy="900" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="1169" pageHeight="827" math="0" shadow="0"><root>${cells}</root></mxGraphModel></diagram>`;
}

const xml = `<?xml version="1.0" encoding="UTF-8"?><mxfile host="app.diagrams.net" modified="2026-10-11T00:00:00.000Z" agent="Codex" version="26.0.14" type="device">${features.map(page).join('')}</mxfile>`;
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, xml, 'utf8');
console.log(out);
console.log(`pages=${features.length}`);
