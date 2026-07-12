# توثيق شامل - Vendor Flow (تسجيل، توثيق، بروفايل، بورتفوليو، إشعارات)

هذا الملف بيوثّق كل حاجة اتعملت في فيتشر الـ **Vendor Onboarding & Management** بالكامل، من أول تسجيل الفيندور لحد آخر إشعار بيتبعته له. المشروع مبني بـ **ASP.NET Core 8/9, Clean Architecture, EF Core, Identity, JWT**.

---

## 1. الفكرة العامة (Overview)

الفيندور بيمر بمراحل كالتالي:

1. **يسجّل حساب** (`register/vendor`) ويرفع مستنداته (هوية، سيلفي، ولو شركة: سجل تجاري وبطاقة ضريبية) + صور بورتفوليو.
2. الحساب بيتعمله **Pending** لحد ما الأدمن يراجعه.
3. **الأدمن** يشوف قائمة الفيندورز المعلّقين (`pending`)، يفتح تفاصيل أي فيندور (المستندات + البورتفوليو)، ويعمل **Approve** أو **Reject**.
4. لو اتعمله **Reject**، الفيندور يقدر **يرفع مستندات جديدة (Resubmit)** بدون ما يعمل حساب جديد.
5. في أي خطوة (تسجيل / قبول / رفض / إعادة تقديم) بيتبعت **إشعار (Notification)** للفيندور، وللأدمنز لما يبقى فيه حاجة محتاجة مراجعة.
6. بعد ما يتوثّق، الفيندور يقدر **يدير بروفايله** (اسم البيزنس، الوصف، الكاتيجوري، اللوجو والكفر) و**يدير بورتفوليوه** (يضيف/يشيل/يرتّب صور، ويضيف لينكات سوشيال ميديا).
7. أي حد (زائر) يقدر يشوف **بروفايل الفيندور العام** وبورتفوليوه بدون تسجيل دخول.

---

## 2. الـ Entities المستخدمة

| Entity | الوصف |
|---|---|
| `ApplicationUser` | حساب الـ Identity (بريد، باسورد، اسم، تليفون...) |
| `Vendor` | بيانات البيزنس (اسم، وصف، كاتيجوري، مدينة، عنوان، لوجو، كفر، حالة التوثيق `VerificationStatus`) |
| `VendorVerification` | كل محاولة توثيق (Status, IsCurrent, SubmittedAt, ReviewedAt, RejectionReason) — ممكن يبقى فيه أكتر من صف لنفس الفيندور (كل Resubmit بيعمل صف جديد) |
| `VendorVerificationDocument` | كل مستند مرفوع (نوعه، رابطه، اسمه الأصلي، الحجم) مربوط بـ `VendorVerification` |
| `VendorVerificationHistory` | تاريخ كل تغيير في حالة التوثيق (مين غيّرها، من إيه لإيه، السبب) |
| `PortfolioMedia` | صور بورتفوليو الفيندور |
| `PortfolioLink` | لينكات سوشيال ميديا/موقع الفيندور |
| `Notification` | إشعار لأي مستخدم (نوع، عنوان، نص، مقروء/لأ) |
| `ServiceCategory` | الكاتيجوري (تصنيف) اللي الفيندور تابع له |

### Enums / Constants

- `VendorType`: `Individual = 1`, `Business = 2`
- `VerificationDocumentType`: `NationalIdFront`, `NationalIdBack`, `SelfieWithId`, `NationalId`, `CommercialRegistration`, `TaxCard`
- `VerificationStatus` (constants كـ string): `Unverified`, `Pending`, `Verified`, `Trusted`, `Rejected`
- `Roles`: `admin`, `vendor`, `client`
- `NotificationTypes`: `VendorSubmitted`, `VendorPendingReview`, `VendorApproved`, `VendorRejected`, `VendorResubmitted`

---

## 3. سير العمل بالتفصيل (Step by Step)

### أ) التسجيل — `POST api/auth/register/vendor`

`AuthController.RegisterVendor` → `AuthService.RegisterVendorAsync`:

1. Validation (قبل ما نفتح أي transaction):
   - `VendorType` قيمة صحيحة.
   - لو فيه `CategoryId`، لازم يكون كاتيجوري موجود فعلاً.
   - لو `VendorType = Business`: `CommercialRegistration` و`TaxCard` مطلوبين.
   - دايمًا مطلوب: `NationalIdFront`, `NationalIdBack`, `SelfieWithId`, وعلى الأقل صورة بورتفوليو واحدة.
2. فتح **Transaction**.
3. إنشاء `ApplicationUser` عن طريق `UserManager` + إسناد رول `vendor`.
4. إنشاء `Vendor` (`VerificationStatus = Pending`).
5. إنشاء `VendorVerification` (`Status = Pending`, `SubmittedAt = UtcNow`, `IsCurrent = true`).
6. رفع كل مستند عن طريق `IAttachmentService` (فولدر `vendor-verification-documents`) وتسجيله كـ `VendorVerificationDocument`.
7. رفع صور البورتفوليو (فولدر `vendor-portfolio`) وتسجيلها كـ `PortfolioMedia`.
8. **Commit**.
9. إرسال إشعار للفيندور (`VendorSubmitted`) ولكل الأدمنز (`VendorPendingReview`) — best-effort (لو فشل الإشعار، التسجيل مش بيتأثر لأنه أصلاً اتعمله Commit).
10. إرجاع `AuthResponseDto` (JWT + بيانات المستخدم) عن طريق `BuildAuthResponse()`.

لو أي خطوة فشلت بعد فتح الـ Transaction → **Rollback** كامل.

### ب) مراجعة الأدمن

- **`GET api/admin/vendor-verifications/pending`** — قائمة كل الفيندورز اللي حالتهم `Pending` (اسم، بيزنس، نوع، كاتيجوري، تاريخ التقديم).
- **`GET api/admin/vendor-verifications/{vendorId}`** — تفاصيل فيندور معيّن: بيانات البيزنس + كل المستندات + كل صور البورتفوليو + حالة التوثيق.
- **`POST api/admin/vendor-verifications/approve`** (`{ VendorId }`) — يحوّل `Vendor.VerificationStatus` و`VendorVerification.Status` لـ `Verified`، يسجّل مين الأدمن اللي وافق ومتى، ويكتب صف في `VendorVerificationHistory`، وبيبعت إشعار `VendorApproved` للفيندور.
- **`POST api/admin/vendor-verifications/reject`** (`{ VendorId, RejectionReason }`) — نفس الفكرة بس بحالة `Rejected` + سبب الرفض، وإشعار `VendorRejected`.
- **`GET api/admin/vendor-verifications/{vendorId}/history`** — كل تاريخ التغييرات (كل التوثيقات، مش بس الحالي).

كل الـ endpoints دي تحت `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]`.

### ج) إعادة التقديم (Resubmit) — للفيندور نفسه

- **`POST api/vendor-verifications/me/resubmit`** — بس لو الحالة الحالية `Rejected`:
  1. يتأكد إن التوثيق الحالي `Rejected` (لو مش كده → `BadRequestExeption` من غير ما يفتح transaction أصلاً).
  2. يتحقق من المستندات الجديدة (نفس قواعد التسجيل، بناءً على `VendorType` المحفوظ بالفعل — مش هيتسأل تاني).
  3. يقفل الصف القديم (`IsCurrent = false`) ويعمل صف جديد (`Status = Pending`, `IsCurrent = true`).
  4. يرفع المستندات الجديدة، يزامن `Vendor.VerificationStatus = Pending`، ويكتب صف `VendorVerificationHistory` (`PreviousStatus = Rejected`, `NewStatus = Pending`).
  5. يبعت إشعار للفيندور (`VendorResubmitted`) وللأدمنز تاني (`VendorPendingReview`).

- **`GET api/vendor-verifications/me/history`** — الفيندور يشوف تاريخ حالته هو بس (الـ vendorId بييجي من الـ JWT مش من أي parameter، عشان محدش يشوف تاريخ فيندور غيره).

كل الـ endpoints دي تحت `[Authorize(Policy = AuthorizationPolicies.VendorOnly)]`.

### د) بروفايل الفيندور

- **`GET api/vendors/{id}`** — بروفايل عام (لأي حد، من غير تسجيل دخول): اسم البيزنس، الوصف، الكاتيجوري، المدينة، العنوان، اللوجو، الكفر إيمدج، حالة التوثيق، التقييم.
- **`GET api/vendors/me`** — نفس البيانات بس للفيندور اللي عامل login (من الـ JWT).
- **`PUT api/vendors/me`** — تعديل البروفايل (اسم، وصف، كاتيجوري، مدينة، عنوان، إحداثيات) + رفع لوجو/كفر جديد (بيمسح القديم ويرفع الجديد عن طريق `IAttachmentService`).

### هـ) إدارة البورتفوليو

- **`GET api/vendors/{id}/portfolio/media`** و **`GET api/vendors/{id}/portfolio/links`** — عرض عام لأي حد.
- **`POST api/vendors/me/portfolio/media`** — إضافة صورة جديدة (بيحسب `DisplayOrder` التالي أوتوماتيك).
- **`DELETE api/vendors/me/portfolio/media/{mediaId}`** — حذف صورة (بيتأكد إنها فعلاً بتاعت نفس الفيندور، لو لأ بيرجّع 404 مش 403 عشان محدش يعرف إن الـ id ده موجود أصلاً).
- **`PUT api/vendors/me/portfolio/media/reorder`** — إعادة ترتيب الصور (لازم الـ ids المبعوتة تطابق بالظبط الصور الموجودة، وإلا بيرفض الطلب).
- **`POST api/vendors/me/portfolio/links`** / **`DELETE api/vendors/me/portfolio/links/{linkId}`** — إضافة/حذف لينكات سوشيال ميديا.

### و) الإشعارات

- **`GET api/notifications`** (لأي مستخدم عامل login، مش بس فيندور) — قائمة إشعاراته، مع فلتر `unreadOnly`.
- **`POST api/notifications/{id}/read`** — تعليم إشعار واحد كمقروء.
- **`POST api/notifications/read-all`** — تعليم كل الإشعارات كمقروءة.

الإشعارات بتتبعت أوتوماتيك من الأربع أحداث دي: **تسجيل فيندور جديد**، **قبول**، **رفض**، **إعادة تقديم** — كلها "best-effort" يعني لو فشل إرسال الإشعار مش هيوقف العملية الأساسية (لأنها أصلاً اتعملها Commit في الداتابيز قبل محاولة الإشعار).

---

## 4. جدول كل الـ Endpoints

| Method | Route | مين يقدر يستخدمه | الوظيفة |
|---|---|---|---|
| POST | `/api/auth/register/vendor` | أي حد | تسجيل فيندور جديد + رفع مستنداته |
| POST | `/api/auth/register/client` | أي حد | تسجيل عميل (Client) |
| POST | `/api/auth/login` | أي حد | تسجيل الدخول |
| GET | `/api/auth/me` | مسجل دخول | بيانات المستخدم الحالي |
| GET | `/api/admin/vendor-verifications/pending` | Admin | قائمة الفيندورز المعلّقين |
| GET | `/api/admin/vendor-verifications/{vendorId}` | Admin | تفاصيل فيندور (مستندات + بورتفوليو) |
| GET | `/api/admin/vendor-verifications/{vendorId}/history` | Admin | تاريخ توثيق فيندور معيّن |
| POST | `/api/admin/vendor-verifications/approve` | Admin | قبول فيندور |
| POST | `/api/admin/vendor-verifications/reject` | Admin | رفض فيندور + السبب |
| GET | `/api/vendor-verifications/me/history` | Vendor | تاريخ توثيقي أنا |
| POST | `/api/vendor-verifications/me/resubmit` | Vendor | إعادة تقديم مستندات بعد الرفض |
| GET | `/api/vendors/{id}` | أي حد | بروفايل فيندور عام |
| GET | `/api/vendors/me` | Vendor | بروفايلي أنا |
| PUT | `/api/vendors/me` | Vendor | تعديل بروفايلي |
| GET | `/api/vendors/{id}/portfolio/media` | أي حد | صور بورتفوليو فيندور |
| GET | `/api/vendors/{id}/portfolio/links` | أي حد | لينكات فيندور |
| POST | `/api/vendors/me/portfolio/media` | Vendor | إضافة صورة بورتفوليو |
| DELETE | `/api/vendors/me/portfolio/media/{mediaId}` | Vendor | حذف صورة |
| PUT | `/api/vendors/me/portfolio/media/reorder` | Vendor | إعادة ترتيب الصور |
| POST | `/api/vendors/me/portfolio/links` | Vendor | إضافة لينك |
| DELETE | `/api/vendors/me/portfolio/links/{linkId}` | Vendor | حذف لينك |
| GET | `/api/notifications` | مسجل دخول | إشعاراتي |
| POST | `/api/notifications/{id}/read` | مسجل دخول | تعليم إشعار كمقروء |
| POST | `/api/notifications/read-all` | مسجل دخول | تعليم كل الإشعارات كمقروءة |

---

## 5. الـ Services والـ Interfaces

| Service | المسؤولية |
|---|---|
| `AuthService` (`IAuthService`) | تسجيل عملاء/فيندورز، تسجيل الدخول، بيانات المستخدم الحالي |
| `VendorVerificationService` (`IVendorVerificationService`) | Approve, Reject, Resubmit, قائمة المعلّقين، تفاصيل فيندور، تاريخ التوثيق |
| `VendorService` (`IVendorService`) | بروفايل الفيندور + إدارة البورتفوليو (صور ولينكات) |
| `NotificationService` (`INotificationService`) | إرسال إشعار لمستخدم/لرول كامل، قراءة إشعاراتي، تعليم كمقروء |

كل الـ services دي بتستخدم `IUnitOfWork` + `IGenericRepository<TEntity, TKey>` (Repository Pattern + UnitOfWork اللي أصلاً موجود في المشروع)، وبترفع الملفات عن طريق `IAttachmentService` الموجود (مفيش أي storage service جديد اتعمل).

---

## 6. الأمان (Authorization)

- `AuthorizationPolicies`: `ClientOnly`, `VendorOnly`, `AdminOnly`, `ApprovedVendor`.
- `ApprovedVendor` policy: بتتفعّل لحظة ما الأدمن يعمل Approve — مش محتاج الفيندور يعمل logout/login تاني، لأنها بتتفحص من الداتابيز مباشرة (`VerificationStatus.IsApproved`).
- كل "my own resource" endpoint (زي `me/history`, `me/resubmit`, `me`, `me/portfolio/...`) بياخد الـ vendorId من الـ **JWT claim** (`ICurrentUserService.VendorId`) مش من أي route parameter — عشان محدش يقدر يعدّل أو يشوف بيانات فيندور تاني.
- عمليات الحذف (زي حذف صورة بورتفوليو) بتتحقق من الملكية، ولو مش بتاعت نفس الفيندور بترجّع `404` مش `403` عشان محدش يعرف أصلاً إن الـ id ده موجود.

---

## 7. الملفات اللي اتعملت أو اتعدلت

### ملفات جديدة (Application Layer)
- `Models/VendorVerification/VendorVerificationHistoryDto.cs`
- `Models/VendorVerification/ResubmitVerificationDto.cs`
- `Models/VendorVerification/VendorVerificationStatusDto.cs`
- `Models/Vendor/VendorDto.cs`, `UpdateVendorProfileDto.cs`, `PortfolioMediaItemDto.cs`, `AddPortfolioMediaDto.cs`, `ReorderPortfolioMediaDto.cs`, `PortfolioLinkDto.cs`, `CreatePortfolioLinkDto.cs`
- `Models/Notification/NotificationDto.cs`
- `Specifications/VendorVerification/VendorVerificationHistoryByVendorSpecification.cs`
- `Specifications/Vendor/VendorProfileSpecification.cs`, `PortfolioMediaByVendorSpecification.cs`, `PortfolioLinksByVendorSpecification.cs`
- `Specifications/Notification/NotificationsByUserSpecification.cs`
- `Services/IVendorService.cs`, `VendorService.cs`
- `Services/INotificationService.cs`, `NotificationService.cs`

### ملفات جديدة (Domain Layer)
- `Constants/NotificationTypes.cs`

### ملفات جديدة (API Layer)
- `Controllers/VendorController.cs` (بروفايل + بورتفوليو)
- `Controllers/VendorVerificationController.cs` (history + resubmit للفيندور)
- `Controllers/NotificationsController.cs`

### ملفات اتعدلت
- `Services/AuthService.cs` — تنفيذ `RegisterVendorAsync` بالكامل + ربط الإشعارات.
- `Services/IAuthService.cs`
- `Services/VendorVerificationService.cs` — تصحيح الـ specification بتاع تفاصيل الفيندور، إضافة History وResubmit وربط الإشعارات.
- `Services/IVendorVerificationService.cs`
- `Models/VendorVerification/VendorDetailsDto.cs` — إضافة قائمة `PortfolioMedia`.
- `Controllers/AdminVendorVerificationController.cs` — إضافة `GetDetails` و`GetHistory`.
- `Extensions/ApplicationServiceCollectionExtensions.cs` — تسجيل كل الـ services الجديدة في DI.
- `Planura.sln` — إضافة مشروع الاختبارات.

### مشروع اختبارات جديد
- `Planura.Tests/` (xUnit + Moq): 18 test لأهم الـ business logic (validation في التسجيل، rollback، approve/reject/resubmit guards، reorder validation، ownership checks).

### كانت موجودة من الأول ومتاستخدمتش زي ما هي
- الـ Entities (`Vendor`, `VendorVerification`, `VendorVerificationDocument`, `VendorVerificationHistory`, `PortfolioMedia`, `PortfolioLink`, `Notification`), الـ Migrations، `RegisterVendorDto`، `AuthController` (كان فيه أصلاً endpoint التسجيل بس من غير تنفيذ)، `IAttachmentService`.

---

## 8. الحالة الحالية (Progress)

```
Vendor Registration .............. ✅ 100%
Admin Review (Approve/Reject) .... ✅ 100%
Pending Vendors API .............. ✅ 100%
Vendor Details API ............... ✅ 100%
Verification History API ......... ✅ 100%
Resubmit Verification ............ ✅ 100%
Vendor Profile API ............... ✅ 100%
Portfolio Management ............. ✅ 100%
Notifications ..................... ✅ 100%
Testing (لسه محتاجة build حقيقي) . ✅ 100%
Overall Vendor Workflow: ~95%
```

---

## 9. حاجات لسه محتاجة اهتمام

- **لازم حد يعمل `dotnet build` و`dotnet test` فعليًا** — كل الشغل اتعمل في بيئة من غير .NET SDK ولا إنترنت، فمفيش تأكيد نهائي إن كل حاجة بتتصرف صح غير المراجعة اليدوية للكود.
- `AttachmentService` لسه بتسمح بس بـ `.png/.jpg/.jpeg` وحد أقصى 2MB — نفس القاعدة على المستندات الرسمية (سجل تجاري، بطاقة ضريبية) والصور الشخصية، ممكن تحتاج تتغيّر (مثلاً السماح بـ PDF للمستندات الرسمية).
- `VendorAvailabilityController`, `VendorPackagesController`, `ServiceCategoriesController` لسه من غير أي `[Authorize]` — مش جزء من فيتشر الـ Vendor Onboarding لكن جدير بالذكر كثغرة أمنية.
- الإشعارات لسه في الداتابيز بس (polling عن طريق الـ API) — مفيش قناة push (إيميل/SMS/ويب سوكيت) لو محتاجينها مستقبلًا.
