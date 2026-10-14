from copy import deepcopy
from docx import Document
from docx.table import Table
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

SRC = r"C:\Users\huyng_jgxkbi0\Downloads\SEP490\Report-3_SRS_completed.docx"

def set_text(p, value):
    # Preserve paragraph properties and the existing run formatting.
    first_rpr = None
    for child in p._p:
        if child.tag == qn('w:r'):
            rpr = child.find(qn('w:rPr'))
            if rpr is not None:
                first_rpr = deepcopy(rpr)
            break
    for child in list(p._p):
        if child.tag != qn('w:pPr'):
            p._p.remove(child)
    for i, line in enumerate(value.split('\n')):
        r = OxmlElement('w:r')
        if first_rpr is not None:
            r.append(deepcopy(first_rpr))
        t = OxmlElement('w:t')
        if line.startswith(' ') or line.endswith(' '):
            t.set(qn('xml:space'), 'preserve')
        t.text = line
        r.append(t)
        p._p.append(r)
        if i < len(value.split('\n')) - 1:
            br = OxmlElement('w:br')
            p._p.append(br)

def clone_paragraph(doc, source_text, new_text):
    source = next(p for p in doc.paragraphs if p.text.strip() == source_text or p.text.strip().startswith(source_text))
    element = deepcopy(source._p)
    # Wrap temporarily only to preserve the source's paragraph/run formatting.
    from docx.text.paragraph import Paragraph
    para = Paragraph(element, doc.part)
    set_text(para, new_text)
    return element

def clone_uc_table(doc, data):
    # Table 34 is the established UC specification layout.
    element = deepcopy(UC_TEMPLATE)
    table = Table(element, doc.part)
    for row, col, text in data:
        set_text(table.cell(row, col).paragraphs[0], text)
    return element

def clone_fields_table(doc, rows):
    # Table 46 is the established screen-field description layout.
    element = deepcopy(FIELDS_TEMPLATE)
    table = Table(element, doc.part)
    for row, col, text in rows:
        set_text(table.cell(row, col).paragraphs[0], text)
    return element

def insert_after(anchor, element):
    anchor.addnext(element)
    return element

def uc_data(uc_id, created_by, actor, secondary, summary, pre, post, dependency, main, alt, exceptions, nfr, questions):
    return [
        (0, 1, uc_id), (1, 1, created_by), (1, 3, '28/09/2026'),
        (2, 1, actor), (2, 3, secondary), (3, 1, summary), (4, 1, pre),
        (5, 1, post), (6, 1, dependency), (7, 1, main), (8, 1, alt),
        (9, 1, exceptions), (10, 1, nfr), (11, 1, questions),
    ]

doc = Document(SRC)
UC_TEMPLATE = deepcopy(doc.tables[34]._tbl)
FIELDS_TEMPLATE = deepcopy(doc.tables[46]._tbl)
AUTH_ANCHOR = doc.tables[38]._tbl
ADMIN_ANCHOR = doc.tables[42]._tbl

forgot_uc = uc_data(
    'UC-AUTH-05 - Forgot & Reset Password', 'HuyNV', 'Guest / Registered User',
    'Google Gmail Notification API (API-02)',
    'As a registered user, I want to request a password reset and set a new password through a verified one-time code so that I can regain secure access when I forget my password.',
    'The user is not logged in.\nThe Forgot Password screen is accessible from the Sign In screen.\nThe user has access to the email address registered to the account.',
    'The account password is replaced with a securely hashed new password.\nAll active reset tokens for the account are invalidated.\nThe system records the reset event and redirects the user to the Sign In screen.',
    'Google Gmail Notification API (API-02) sends the password-reset OTP.',
    'User selects Forgot Password from the Sign In screen.\nSystem displays the Forgot Password screen.\nUser enters the registered email address and selects Send Verification Code.\nSystem accepts the request without disclosing whether the email is registered, generates a single-use OTP with an expiry time, and sends it to the email address when an account exists.\nUser enters the OTP and selects Verify.\nSystem validates the OTP and displays the Reset Password form.\nUser enters New Password and Confirm New Password, then selects Reset Password.\nSystem validates the password policy and confirmation.\nSystem hashes and stores the new password, invalidates active reset tokens and sessions as configured, records an audit event, and displays a success message.\nSystem redirects the user to the Sign In screen.',
    'AF1 - Resend OTP (at Step 5): User selects Resend Code after the configured wait period. The system invalidates the previous OTP, generates a new OTP, and sends it to the registered email.\nAF2 - Cancel Reset: User selects Cancel or Back to Sign In. The system discards the reset flow without changing the password.\nAF3 - Password Policy Failure (at Step 8): The system displays field-level validation messages and allows the user to enter a compliant password.',
    'E1 - Invalid or Expired OTP (at Step 6): The system displays an error, does not reveal account details, and allows the user to request a new code.\nE2 - OTP Retry Limit Reached: The system blocks further verification attempts for the configured period and records a security event.\nE3 - Email Delivery Failure (at Step 4): The system logs the failure and displays a generic retry message.\nE4 - Password Update Failure (at Step 9): The system rolls back the update, keeps the current password unchanged, and displays a generic error message.',
    'The screen shall respond within 3 seconds under normal operating conditions.\nOTP values shall be single-use, expire within 15 minutes, and be rate-limited.\nThe system shall not disclose whether an email address is registered.\nPasswords shall satisfy the platform password policy and be stored only as a secure hash.\nAll reset requests and successful resets shall be audit logged.\nCredentials and OTPs shall be transmitted only over HTTPS.',
    'What is the configured OTP resend delay and maximum verification-attempt limit?\nShould all existing sessions be revoked immediately after a successful password reset?'
)

user_uc = uc_data(
    'UC-ADM-01 - User Management Screen', 'HuyNV', 'Administrator', 'None',
    'As an Administrator, I want to search, review, and manage user accounts so that account access and verification status remain accurate and secure.',
    'The Administrator is signed in with an Active Administrator account.\nThe Administrator has permission to access User Management.',
    'The selected account status or lock state is updated when an allowed action is confirmed.\nThe system records an audit log for every account-status or unlock action.',
    'GBR-06 and GBR-07; the Administrator cannot manage their own account status or lock state.',
    'Administrator opens User Management.\nSystem displays paginated user records with search, role, and account-status filters.\nAdministrator enters optional search criteria and selects Search or changes a filter.\nSystem returns matching accounts and their user/entity name, email, phone number, role, account status, KYC status, and creation date.\nAdministrator selects View Details to inspect a selected account.\nAdministrator selects Suspend, Activate, or Unlock for an eligible account.\nSystem displays a confirmation dialog; a reason is required for a status change.\nAdministrator confirms the action.\nSystem validates authority and business rules, updates the account, records the action in the audit log, refreshes the list, and displays a confirmation message.',
    'AF1 - No Search Results: System displays No search results and preserves the selected filters.\nAF2 - View Details: System opens the User Details screen without modifying account data.\nAF3 - Cancel Action: Administrator cancels the confirmation dialog; no account data is changed.',
    'E1 - Unauthorized Access: System denies access and redirects to Sign In or returns 403.\nE2 - Self-Management Attempt: System blocks the action and displays the self-suspension prevention message.\nE3 - Concurrent Update: System reloads the latest account state and asks the Administrator to review and confirm again.\nE4 - Update Failure: System makes no change, logs the error, and displays a generic error message.',
    'User-list search and filter results shall load within 3 seconds under normal operating conditions.\nOnly authorized Administrators may view user data or execute account actions.\nStatus changes require a reason, confirmation, and an audit record containing actor, date/time, previous status, and new status.\nThe list shall display 10 records per page by default.',
    'Which Administrator roles, if any, may access KYC documents from User Details?'
)

category_uc = uc_data(
    'UC-ADM-03 - Product Categories Management Screen', 'HuyNV', 'Administrator', 'None',
    'As an Administrator, I want to create, view, update, activate, deactivate, and delete product categories so that marketplace products are consistently classified.',
    'The Administrator is signed in with an Active Administrator account.\nThe Administrator has permission to access Product Category Management.',
    'A category is created, updated, activated, deactivated, or deleted only after validation succeeds.\nThe system records an audit log for the category change.',
    'GBR-09 and GBR-10; category names are unique within the same parent category and categories containing active products cannot be deleted.',
    'Administrator opens Product Category Management.\nSystem displays the category list with search, status, and parent-category filters.\nAdministrator optionally searches or filters categories.\nSystem displays matching category details, including image, parent, priority, status, product count, and creation date.\nAdministrator selects New Category or Edit.\nSystem displays Product Category Details.\nAdministrator enters or updates category name, optional parent, description, image, priority, and status, then selects Save.\nSystem validates the inputs and uniqueness within the selected parent category.\nSystem creates or updates the category, records an audit log, and displays a success message.\nAdministrator may select Activate or Deactivate; after confirmation, the system updates the category availability and refreshes the list.',
    'AF1 - Cancel Edit: Administrator selects Cancel; system returns to the Category List without saving.\nAF2 - Delete Category: Administrator selects Delete. System checks for active products and, when allowed, displays a confirmation dialog before deleting the category.\nAF3 - Root Category: Administrator leaves Parent Category empty; system creates or updates the category as a root category.',
    'E1 - Duplicate Category Name: System rejects the save and displays a field-level error.\nE2 - Active Products Exist: System blocks deletion and explains that active products must be reassigned or deactivated first.\nE3 - Invalid Image Upload: System rejects unsupported formats or files larger than 2 MB and retains other entered data.\nE4 - Unauthorized Access or Update Failure: System denies the action or leaves the category unchanged, logs the error, and displays a generic error message.',
    'Category-list search and save operations shall complete within 3 seconds under normal operating conditions.\nOnly authorized Administrators may manage categories.\nThe system shall validate JPG, JPEG, and PNG uploads up to 2 MB.\nDestructive actions require confirmation and category changes shall be audit logged.\nThe list shall display 10 records per page by default.',
    'Can a category with inactive products be deleted, or must all assigned products be reassigned first?'
)

# Authentication: insert the new UC immediately after the existing Change Password UC table.
auth_anchor = AUTH_ANCHOR
auth_anchor = insert_after(auth_anchor, clone_paragraph(doc, '1.5 UC-AUTH-06 Change Password', '1.6 UC-AUTH-05 - Forgot & Reset Password'))
auth_anchor = insert_after(auth_anchor, clone_uc_table(doc, forgot_uc))

# Administration: insert the two management UCs after the existing moderation UC table.
admin_anchor = ADMIN_ANCHOR
admin_anchor = insert_after(admin_anchor, clone_paragraph(doc, '4.1 Product Listing Moderation Screen', '4.2 UC-ADM-01 - User Management Screen'))
admin_anchor = insert_after(admin_anchor, clone_uc_table(doc, user_uc))
admin_anchor = insert_after(admin_anchor, clone_paragraph(doc, '4.1 Product Listing Moderation Screen', '4.3 UC-ADM-03 - Product Categories Management Screen'))
admin_anchor = insert_after(admin_anchor, clone_uc_table(doc, category_uc))

# Functional requirements: add a new source-derived screen section before Marketplace.
marketplace = next(p for p in doc.paragraphs if p.text.strip() == '2. MarketPlace')._p
fr_anchor = marketplace
for source_text, new_text in [
    ('1.3 Change Password', '1.4 Forgot & Reset Password'),
    ('1.3.1 Change Password', '1.4.1 Forgot & Reset Password Screen'),
    ('This screen allows an Authenticated User to change their account password.',
     'This screen allows a Guest or registered user to request a password reset using a registered email address, verify a one-time password (OTP), and set a new password. The system protects account privacy by returning the same request acknowledgement whether or not an email address is registered. The OTP is single-use, time-limited, and rate-limited. After successful verification, the system validates the new password against the password policy, stores it as a secure hash, invalidates the reset token, and directs the user to sign in with the new password.'),
]:
    fr_anchor = insert_after(fr_anchor, clone_paragraph(doc, source_text, new_text))

fields = [
    (1, 0, 'Header'), (1, 1, 'The system header displaying the Nông Sản Việt logo and platform slogan.'),
    (2, 0, 'Page Title'), (2, 1, 'Displays Forgot Password, Verify Code, or Reset Password according to the current reset step.'),
    (3, 0, 'Instruction Text'), (3, 1, 'Displays concise guidance for requesting and completing a password reset.'),
    (4, 0, 'Reset Request'),
    (5, 0, 'Registered Email'), (5, 1, 'Required email input. The system validates standard email format without disclosing whether the email is registered.'),
    (6, 0, 'Send Verification Code Button'), (6, 1, 'Submits the email address and requests a password-reset OTP. The system applies configured rate limits.'),
    (7, 0, 'OTP Verification Code'), (7, 1, 'Required code input. The OTP is single-use and expires within 15 minutes.'),
    (8, 0, 'Verify / Resend Code Button'), (8, 1, 'Verifies the OTP or requests a replacement OTP after the configured resend delay; the previous OTP is invalidated.'),
    (9, 0, 'Reset Password'),
    (10, 0, 'New Password'), (10, 1, 'Required masked password input. The value must satisfy the password policy and must not be the same as the current password.'),
    (11, 0, 'Confirm New Password'), (11, 1, 'Required masked password input. The value must exactly match New Password.'),
    (12, 0, 'Reset Password Button / Validation Message'), (12, 1, 'Validates all fields, securely updates the password, invalidates the reset token, redirects to Sign In, and displays validation errors when necessary.'),
]
fr_anchor = insert_after(fr_anchor, clone_fields_table(doc, fields))

doc.save(SRC)
print(SRC)
