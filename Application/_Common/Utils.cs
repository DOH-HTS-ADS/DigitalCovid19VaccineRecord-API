using Application.Common.Interfaces;
using Application.Options;
using Application.VaccineCredential.Queries.GetVaccineStatus;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Application.Common
{
    public class Utils
    {
        public static readonly ImmutableDictionary<string, string> VaccineTypeNames = new Dictionary<string, string>
        {
            { "207", "Moderna" },
            { "208", "Pfizer" },
            { "210", "AstraZeneca" },
            { "211", "Novavax" },
            { "212", "J&J" },
            { "213", "COVID-19, unspecified" },
            { "218", "Pfizer" }
        }.ToImmutableDictionary();

        private static AppSettings _appSettings;

        private static int messageCalls = 0;

        //private static int noMatchCalls = 0;
        public Utils(AppSettings appSettings)
        {
            _appSettings = appSettings;
        }

        public static int ValidatePin(string pin)
        {
            //Business Rules:  1)  4 digit pin
            //    2)  Not 4 or more of the same # eg:  0000,1111 (not allow)
            //    3)  No more than 3 consecutive #, eg: 1234 (not allow)
            if (pin == null)
            {
                return 1;
            }
            else if (pin.Length != 4)
            {
                return 2;
            }
            else if (!Int32.TryParse(pin, out _))
            {
                return 3;
            }
            //    2)  Not 4 or more of the same # eg:  1111 (not allow)
            else if (ContainsDupsChars(pin, 4))
            {
                return 4;
            }
            //   3)  No consecutive #, eg: 1234 (not allow)
            else if (HasConsecutive(pin, 4))
            {
                return 5;
            }
            else
            {
                return 0;
            }
        }

        public static bool ContainsDupsChars(string s, int max)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            int cnt = 0;
            for (int i = 0; i < s.Length - 1 && cnt < max; ++i)
            {
                var charI = s[i];
                cnt = s.Count(c => c == charI);
                if (cnt >= max)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasConsecutive(string s, int max)
        {
            int cnt = 0;
            for (int i = 0; i < s.Length - 1; i++)
            {
                var chr1 = s[i];
                var chr2 = s[i + 1];
                if (chr1 + 1 == chr2)
                {
                    cnt++;
                }
                else
                {
                    cnt = 0;
                }
                if (cnt >= max - 1)
                {
                    break;
                }
            }

            return cnt >= max - 1;
        }

        public static string ParseLotNumber(string s)
        {   // Check if lot number is Alpha numeric
            if (s == null) { return null; }

            var regex = "^[a-zA-Z0-9-]*$";
            var regexContainsNumber = "[\\d]";
            var tokens = s.Split(" ");

            foreach (var t in tokens)
            {
                if (Regex.IsMatch(t, regex) && Regex.IsMatch(t, regexContainsNumber))
                {
                    return t;
                }
            }

            return null;
        }

        public static string TrimString(string s, int i)
        {
            if (s == null) { return null; }

            if (s.Length > i)
            {
                s = s.Substring(0, i);
            }
            return s;
        }

        //returns 0 if cached
        //        1 if not in db
        //        2 if email send success
        //        3 if sms send successs
        //        4 if email error
        //        5 is sms error
        public static async Task<int> ProcessStatusRequest(AppSettings _appSettings, ILogger logger, IEmailService _emailService, SendGridSettings _sendGridSettings, IMessagingService _messagingService, IAesEncryptionService _aesEncryptionService, GetVaccineCredentialStatusQuery request, IAzureSynapseService _azureSynapseService, SqlConnection conn, CancellationToken cancellationToken, long tryCount = 1)
        {
            Interlocked.Increment(ref messageCalls);
            int ret = 0;

            var smsRecipient = request.PhoneNumber;
            if (!string.IsNullOrWhiteSpace(_appSettings.DeveloperSms))
            {
                smsRecipient = _appSettings.DeveloperSms;
                logger.LogInformation($"OVERRIDE: Sending to developer SMS instead of request.PhoneNumber; request.Id={request.Id}");
            }
            var emailRecipient = request.EmailAddress;
            if (!string.IsNullOrWhiteSpace(_appSettings.DeveloperEmail))
            {
                emailRecipient = _appSettings.DeveloperEmail;
                logger.LogInformation($"OVERRIDE: Sending to developer email instead of request.EmailAddress; request.Id={request.Id}");
            }

            // Get Vaccine Credential
            string response;
            response = await _azureSynapseService.GetVaccineCredentialStatusAsync(request, cancellationToken);

            var logMessage = $"searchCriteria= {Sanitize(request.FirstName.Substring(0, 1))}.{Sanitize(request.LastName.Substring(0, 1))}. {((DateTime)request.DateOfBirth).ToString("MM/dd/yyyy")} {Sanitize(request.PhoneNumber)}{request.EmailAddress} {Sanitize(request.Pin)} response={Sanitize(response)}";

            if (!string.IsNullOrEmpty(response))
            {
                //Generate link url with the GUID and send text or email based on the request preference.
                //Encyrpt the response with  aesencrypt
                var code = DateTime.Now.Ticks + "~" + request.Pin + "~" + response;
                var encrypted = _aesEncryptionService.EncryptGcm(code, _appSettings.CodeSecret);
                logger.LogInformation($"ENCRYPTION: encrypted={encrypted}; request.Id={request.Id}");

                var url = $"{_appSettings.WebUrl}/qr/{request.Language}/{encrypted}";

                //Twilio for SMS.
                if (!string.IsNullOrEmpty(request.PhoneNumber))
                {
                    ret = 3;
                    var messageId = await _messagingService.SendMessageAsync(smsRecipient, FormatSms2(url, request.Language), request.Id, cancellationToken);
                    messageId = await _messagingService.SendMessageAsync(smsRecipient, FormatSms(Convert.ToInt32(_appSettings.LinkExpireHours), request.Language), request.Id, cancellationToken);

                    if (string.IsNullOrEmpty(messageId))
                    {
                        ret = 5;
                    }
                    else if (messageId == "FAILED")
                    {
                        ret = 6;
                    }
                }

                //SendGrid for email
                if (!string.IsNullOrEmpty(request.EmailAddress))
                {
                    ret = 2;
                    var message = new SendGridMessage();
                    message.AddTo(emailRecipient, $"{UppercaseFirst(request.FirstName)} {UppercaseFirst(request.LastName)}");
                    message.SetFrom(_sendGridSettings.SenderEmail, _sendGridSettings.Sender);
                    message.SetSubject("Digital COVID-19 Verification Record");
                    message.PlainTextContent = FormatSms2(url, request.Language) + "/n" + FormatSms(Convert.ToInt32(_appSettings.LinkExpireHours), request.Language); ;
                    message.HtmlContent = FormatHtml(url, request.Language, Convert.ToInt32(_appSettings.LinkExpireHours), _appSettings.WebUrl, _appSettings.CDCUrl, _appSettings.VaccineFAQUrl, _appSettings.CovidWebUrl, _appSettings.EmailLogoUrl);

                    if (!(await _emailService.SendEmailAsync(message, emailRecipient, request.Id)))
                    {
                        ret = 4;
                    }
                }
            }
            else
            {
                ret = 1;

                //Email sms request that we could not find you
                if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
                {
                    if (_appSettings.SendNotFoundSms != "0")
                    {
                        await _messagingService.SendMessageAsync(smsRecipient, FormatNotFoundSms(request.Language, _appSettings.SupportPhoneNumber), request.Id, cancellationToken);
                    }
                }
                else
                {
                    if (_appSettings.SendNotFoundEmail != "0")
                    {
                        var message = new SendGridMessage();
                        message.AddTo(emailRecipient, $"{UppercaseFirst(request.FirstName)} {UppercaseFirst(request.LastName)}");
                        message.SetFrom(_sendGridSettings.SenderEmail, _sendGridSettings.Sender);
                        message.SetSubject("Digital COVID-19 Vaccine Record");
                        message.PlainTextContent = FormatNotFoundSms(request.Language, _appSettings.SupportPhoneNumber);
                        message.HtmlContent = FormatNotFoundHtml(request.Language, _appSettings.WebUrl, _appSettings.ContactUsUrl, _appSettings.VaccineFAQUrl, _appSettings.CovidWebUrl, _appSettings.EmailLogoUrl);
                        await _emailService.SendEmailAsync(message, emailRecipient, request.Id);
                    }
                }
            }

            if (tryCount <= 1)
            {
                switch (ret)
                {
                    case 0:
                        logger.LogInformation($"CACHEDREQUEST: {logMessage}; request.Id={request.Id}");
                        break;

                    case 1:
                        logger.LogInformation($"BADREQUEST-NOTFOUND: {logMessage}; request.Id={request.Id}");
                        break;

                    case 2:
                        logger.LogInformation($"VALIDREQUEST-EMAILSENT: {logMessage}; request.Id={request.Id}");
                        break;

                    case 3:
                        logger.LogInformation($"VALIDREQUEST-SMSSENT: {logMessage}; request.Id={request.Id}");
                        break;

                    case 4:
                        logger.LogWarning($"VALIDREQUEST-EMAILFAILED: {logMessage}; request.Id={request.Id}");
                        break;
                    //case 5:
                    case 6:
                        logger.LogWarning($"VALIDREQUEST-SMSFAILED: {logMessage}; request.Id={request.Id}");
                        break;
                    default:
                        break;
                }
            }
            else
            {
                logger.LogInformation($"RETRY: cnt={tryCount}; ret={ret}; {logMessage}; request.Id={request.Id}");
            }

            return ret;
        }

        /*
         es: Spanish
         cn: Chinese Simplified
         tw: Chinese Traditional
         kr: Korean
         vi: Vietnamese
         ae: Arabic
         ph: Tagalog
         */
        public static string FormatSms(int linkExpireHours, string lang)
        {
            return lang switch
            {
                "es" => $"Gracias por visitar el sistema de registro digital de verificación de la COVID-19. El enlace para recuperar su verificación de COVID-19 es válido por {linkExpireHours} horas. Una vez que acceda y se guarde en su dispositivo, el código QR no vencerá.",
                "zh-CN" => $"欢迎访问数字 COVID-19 验证记录系统。用于检索您 COVID-19 验证的链接在 {linkExpireHours} 小时内有效。在您获取到 QR 码并将其储存到您的设备后，此 QR 码将不会过期。",
                "zh-TW" => $"歡迎造訪數位 COVID-19 驗證記錄系統。用於檢索您的 COVID-19 驗證的連結在 {linkExpireHours} 小時內有效。一旦您存取 QR 代碼並將其儲存到您的裝置後，此 QR 代碼將不會過期。",
                "ko" => $"디지털 COVID-19 인증 기록 시스템을 방문해 주셔서 감사합니다. COVID-19 인증을 조회하는 링크는 {linkExpireHours}시간 동안 유효합니다. 확인하고 기기에 저장하면 QR 코드는 만료되지 않습니다.",
                "vi" => $"Cảm ơn bạn đã truy cập vào hệ thống Hồ sơ Xác nhận COVID-19 kỹ thuật số. Đường liên kết để truy xuất thông tin xác nhận COVID-19 của bạn có hiệu lực trong vòng {linkExpireHours} giờ. Sau khi đã truy cập và lưu vào thiết bị của bạn, mã QR sẽ không hết hạn.",
                "ar" => $"شكرًا لك على زيارة نظام سجل التحقق الرقمي من فيروس كوفيد-19. يظل رابط الحصول على التحقق من فيروس كوفيد-19 الخاص بك صالحًا لمدة 24 ساعة. وب{linkExpireHours}رد الوصول إليه وحفظه على جهازك، لن تنتهي صلاحية رمز الاستجابة السريعة (QR).",
                "tl" => $"Salamat sa pagbisita sa system ng Digital na Rekord sa Pagberipika ng Pagpapabakuna sa COVID-19. May bisa ang link para makuha ang iyong pagberipika ng pagpapabakuna sa COVID-19 nang {linkExpireHours} na oras. Kapag na-aaccess at na-save na ito sa iyong device, hindi mag-e-expire ang QR code.",            
                _ => $"Thank you for visiting the Digital COVID-19 Verification Record system. The link to retrieve your COVID-19 verification is valid for {linkExpireHours} hours. Once accessed and saved to your device, the QR code will not expire.",
            };
        }

        public static string FormatSms2(string url, string lang)
        {
            return lang switch
            {
                "es" => $"{url}",
                "zh-CN" => $"{url}",
                "zh-TW" => $"{url}",
                "ko" => $"{url}",
                "vi" => $"{url}",
                "ar" => $"{url}",
                "tl" => $"{url}",
                _ => $"{url}",
            };
        }

        public static string FormatHtml(string url, string lang, int linkExpireHours, string webUrl, string cdcUrl, string vaccineFAQUrl, string covidWebUrl, string emailLogoUrl)
        {
            if (String.IsNullOrEmpty(url))
                throw new Exception("url is null");

            if (String.IsNullOrEmpty(lang))
                throw new Exception("lang is null");

            if (String.IsNullOrEmpty(webUrl))
                throw new Exception("webUrl is null");

            if (String.IsNullOrEmpty(cdcUrl))
                throw new Exception("cdcUrl is null");

            if (String.IsNullOrEmpty(vaccineFAQUrl))
                throw new Exception("vaccineFAQUrl is null");

            if (String.IsNullOrEmpty(covidWebUrl))
                throw new Exception("covidWebUrl is null");

            if (String.IsNullOrEmpty(emailLogoUrl))
                throw new Exception("emailLogoUrl is null");

            return lang switch
            {
                "es" => $"<img src='{webUrl}/imgs/MyTurn-logo.png'><br/>" +
                    $"<h3 style='color: #f06724'>Registro digital de verificación de la COVID-19</h3>" +
                    $"<p>Gracias por visitar el sistema de registro digital de verificación de la COVID-19. El enlace para recuperar su código de registro de vacunación de la COVID-19 es válido por {linkExpireHours} horas. Una vez que acceda y se guarde en su dispositivo, el código QR no vencerá.</p>" +
                    $"<p><a href='{url}'>Consulte los registros de vacunación</a></p>" +
                    $"<p>Obtenga más información sobre cómo <a href='{cdcUrl}'>protegerse usted y proteger a otros</a> de los Centros para el Control y la Prevención de Enfermedades.</p>" +
                    $"<p><b>¿Tiene alguna pregunta?</b></p>" +
                    $"<p>Visite nuestra página de <a href='{vaccineFAQUrl}'>preguntas frecuentes</a> para obtener más información sobre el registro digital de vacunación contra la COVID-19.</p>" +
                    $"<p><b>Manténgase informado.</b></p>" +
                    $"<p><a href='{covidWebUrl}'>Consulte la información más reciente</a> sobre la COVID-19.</p><br/>" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Correo electrónico oficial del Departamento de Salud del Estado de Washington</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}'></p></footer>",
                "zh-CN" => $"<img src='{webUrl}/imgs/MyTurn-logo.png'><br/>" +
                    $"<h3 style='color: #f06724'>数字 COVID-19 验证记录</h3>" +
                    $"<p>欢迎访问数字 COVID-19 验证记录系统。用于检索您 COVID-19 疫苗记录码的链接在 {linkExpireHours} 小时内有效。在您获取到 QR 码并将其储存到您的设备后，此 QR 码将不会过期。</p>" +
                    $"<p><a href='{url}'>查看疫苗记录</a></p>" +
                    $"<p>从 Centers for Disease Control and Prevention（CDC，疾病控制与预防中心）了解更多关于如何<a href='{cdcUrl}'>保护自己和他人</a> 的相关信息。</p>" +
                    $"<p><b>仍有疑问？</b></p>" +
                    $"<p>请访问我们的常见问题解答 (<a href='{vaccineFAQUrl}'>FAQ</a>) 页面，以了解有关您的数字 COVID-19 疫苗记录的更多信息。</p>" +
                    $"<p><b>保持关注。</b></p>" +
                    $"<p><a href='{covidWebUrl}'>查看 COVID-19 最新信息</a>。</p><br/>" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health (华盛顿州卫生部）官方电子邮件</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}'></p></footer>",
                "zh-TW" => $"<img src='{webUrl}/imgs/MyTurn-logo.png'><br/>" +
                    $"<h3 style='color: #f06724'>數位 COVID-19 驗證記錄</h3>" +
                    $"<p>歡迎造訪數位 COVID-19 驗證記錄系統。用於檢索您的 COVID-19 疫苗記錄碼的連結在 {linkExpireHours} 小時內有效。一旦您存取 QR 代碼並將其儲存到您的裝置後，此 QR 代碼將不會過期。</p>" +
                    $"<p><a href='{url}'>檢視疫苗記錄</a></p>" +
                    $"<p>從 Centers for Disease Control and Prevention（CDC，疾病控制與預防中心）瞭解更多關於如何<a href='{cdcUrl}'>保護自己和他人</a> 的相關資訊。</p>" +
                    $"<p><b>仍有疑問？</b></p>" +
                    $"<p>請造訪我們的常見問題解答 (<a href='{vaccineFAQUrl}'>FAQ</a>)頁面，以瞭解有關您的數位 COVID-19 疫苗記錄的更多資訊。</p>" +
                    $"<p><b>保持關注。</b></p>" +
                    $"<p><a href='{covidWebUrl}'>檢視 COVID-19 最新資訊</a>。</p><br/>" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health （華盛頓州衛生部）官方電子郵件 </p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}'></p></footer>",
                "ko" => $"<img src='{webUrl}/imgs/MyTurn-logo.png'><br/>" +
                    $"<h3 style='color: #f06724'>디지털 COVID-19 인증 기록</h3>" +
                    $"<p>디지털 COVID-19 인증 기록 시스템을 방문해 주셔서 감사합니다. COVID-19 백신 기록 코드를 조회하는 링크는 {linkExpireHours}시간 동안 유효합니다. 확인하고 기기에 저장하면 QR 코드는 만료되지 않습니다. </p>" +
                    $"<p><a href='{url}'>백신 기록 보기</a></p>" +
                    $"<p>Centers for Disease Control and Prevention(질병통제예방센터)에서 <a href='{cdcUrl}'>나와 타인을 보호</a> 하는 방법에 대해 자세히 확인해 보십시오.</p>" +
                    $"<p><b>궁금한 사항이 있으신가요?</b></p>" +
                    $"<p>디지털 COVID-19 백신 기록에 대해 자세히 알아보려면 자주 묻는 질문 (<a href='{vaccineFAQUrl}'>FAQ</a>) 페이지를 참조해 주십시오.</p>" +
                    $"<p><b>최신 정보를 확인하십시오.</b></p>" +
                    $"<p>COVID-19 관련 <a href='{covidWebUrl}'>최신 정보 보기</a></p><br/>" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health (워싱턴주 보건부) 공식 이메일</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}'></p></footer>",
                "vi" => $"<img src='{webUrl}/imgs/MyTurn-logo.png'><br/>" +
                    $"<h3 style='color: #f06724'>Hồ sơ Xác nhận COVID-19 kỹ thuật số</h3>" +
                    $"<p>Cảm ơn bạn đã truy cập vào hệ thống Hồ sơ Xác nhận COVID-19 kỹ thuật số. Đường liên kết để truy xuất mã hồ sơ vắc-xin COVID-19 của bạn có hiệu lực trong vòng {linkExpireHours} giờ. Sau khi đã truy cập và lưu vào thiết bị của bạn, mã QR sẽ không hết hạn.</p>" +
                    $"<p><a href='{url}'>Xem Hồ sơ Vắc-xin</a></p>" +
                    $"<p>Tìm hiểu thêm về cách <a href='{cdcUrl}'>tự bảo vệ mình và bảo vệ người khác</a> từ Centers for Disease Control and Prevention (CDC, Trung Tâm Kiểm Soát và Phòng Ngừa Dịch Bệnh).</p>" +
                    $"<p><b>Có câu hỏi?</b></p>" +
                    $"<p>Truy cập vào trang Các Câu Hỏi Thường Gặp (<a href='{vaccineFAQUrl}'>FAQ</a>) để tìm hiểu thêm về Hồ Sơ Vắc-xin COVID-19 kỹ thuật số của bạn.</p>" +
                    $"<p><b>Luôn cập nhật thông tin.</b></p>" +
                    $"<p><a href='{covidWebUrl}'>Xem thông tin mới nhất</a> về COVID-19.</p><br/>" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Email chính thức của Washington State Department of Health (Sở Y Tế Tiểu Bang Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}'></p></footer>",
                "ar" => $"<img dir='rtl' src='{webUrl}/imgs/MyTurn-logo.png'><br/>" +
                    $"<h3 dir='rtl' style='color: #f06724'>سجل التحقق الرقمي من فيروس كوفيد-19</h3>" +
                    $"<p dir='rtl'>شكرًا لك على زيارة نظام سجل التحقق الرقمي من فيروس كوفيد-19. يظل رابط الحصول على رمز سجل لقاح فيروس كوفيد-19 صالحًا لمدة {linkExpireHours} ساعة. وبمجرد الوصول إليه وحفظه على جهازك، لن تنتهي صلاحية رمز الاستجابة السريعة (QR).</p>" +
                    $"<p dir='rtl'><a href='{url}'>عرض سجل اللقاح</a></p>" +
                    $"<p dir='rtl'>تتعرَّف على مزيدٍ من المعلومات  عن <a href='{cdcUrl}'>كيفية حماية نفسك والآخرين</a> من خلال Centers for Disease Control and Prevention ( CDC، مراكز مكافحة الأمراض والوقاية منها).</p>" +
                    $"<p dir='rtl'><b>هل لديك أي أسئلة؟</b></p>" +
                    $"<p dir='rtl'>قم بزيارة صفحة <a href='{vaccineFAQUrl}'>الأسئلة</a> الشائعة  الخاصة بنا للاطلاع على مزيدٍ من المعلومات حول السجل الرقمي للقاح كوفيد-19 الخاص بك.</p>" +
                    $"<p dir='rtl'><b>ابقَ مطلعًا.</b></p>" +
                    $"<p dir='rtl'><a href='{covidWebUrl}'>عرض آخر المعلومات</a> عن فيروس كوفيد-19.</p>" +
                    $"<hr>" +
                    $"<footer><p dir='rtl' style='text-align:center'>البريد الإلكتروني الرسمي الخاص بـ Washington State Department of Health (إدارة الصحة في ولاية واشنطن)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}'></p></footer>",
                "tl" => $"<img src='{webUrl}/imgs/MyTurn-logo.png'><br/>" +
                    $"<h3 style='color: #f06724'>Digital na Rekord sa Pagberipika ng Pagpapabakuna sa COVID-19</h3>" +
                    $"<p>Salamat sa pagbisita sa system ng Digital na Rekord sa Pagberipika ng Pagpapabakuna sa COVID-19. May bisa ang link para makuha ang iyong code sa pagberipika ng bakuna sa COVID-19 nang {linkExpireHours} na oras. Kapag na-aaccess at na-save na ito sa iyong device, hindi mag-e-expire ang QR code.</p>" +
                    $"<p><a href='{url}'>Tingnan ang Rekord ng Bakuna</a></p>" +
                    $"<p>Matuto pa tungkol sa kung paano <a href='{cdcUrl}'>protektahan ang iyong sarili at ang ibang tao</a> mula sa impormasyon mula sa Centers for Disease Control and Prevention (Mga Sentro sa Pagkontrol at Pag-iwas sa Sakit).</p>" +
                    $"<p><b>May mga tanong?</b></p>" +
                    $"<p>Bisitahin ang aming page ng Mga Madalas Itanong (<a href='{vaccineFAQUrl}'>FAQ</a>) para matuto pa tungkol sa iyong Digital na Rekord ng Bakuna sa COVID-19.</p>" +
                    $"<p><b>Manatiling May Kaalaman.</b></p>" +
                    $"<p><a href='{covidWebUrl}'>Tingnan ang pinakabagong impormasyon</a> tungkol sa COVID-19.</p><br/>" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Opisyal na Email ng Washington State Department of Health (Departamento ng Kalusugan ng Estado ng Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}'></p></footer>",
                _ => $"<img src='{webUrl}/imgs/MyTurn-logo.png'><br/>" +
                  $"<h3 style='color: #f06724'>Digital COVID-19 Verification Record</h3>" +
                  $"<p>Thank you for visiting the Digital COVID-19 Verification Record system. The link to retrieve your COVID-19 vaccine record code is valid for {linkExpireHours} hours. Once accessed and saved to your device, the QR code will not expire.</p>" +
                  $"<p><a href='{url}'>View Vaccine Record</a></p>" +
                  $"<p>Learn more about how to <a href='{cdcUrl}'>protect yourself and others</a> from the Centers for Disease Control and Prevention.</p>" +
                  $"<p><b>Have questions?</b></p>" +
                  $"<p>Visit our <a href='{vaccineFAQUrl}'>(FAQ)</a> page to learn more about your Digital COVID-19 Verification Record.</p>" +
                  $"<p><b>Stay Informed.</b></p>" +
                  $"<p><a href='{covidWebUrl}'>View the latest information</a> on COVID-19.</p><br/>" +
                  $"<hr>" +
                  $"<footer><p style='text-align:center'>Official Washington State Department of Health e-mail</p>" +
                  $"<p style='text-align:center'><img src='{emailLogoUrl}'></p></footer>"
            };
        }

        public static string FormatNotFoundSms(string lang, string phoneNumber)
        {
            return lang switch
            {
                "es" => $"Recientemente solicitó un registro digital de verificación de la COVID-19 del estado. Desafortunadamente, la información que ingresó no coincide con la que tenemos en nuestro sistema. Comuníquese al {phoneNumber} y, luego, presione asterisco (#) para obtener ayuda a fin de que coincida la información de contacto con los registros.",
                "zh-CN" => $"您最近向州政府请求过数字 COVID-19 验证记录。很遗憾，您提供的信息与我们系统中的信息不符。请拨打 {phoneNumber} 与我们联系，按 # 可获取将您的记录与您的联系信息进行匹配的援助。",
                "zh-TW" => $"您最近向州政府請求過數位 COVID-19 驗證記錄。很遺憾，您提供的資訊與我們系統中的資訊不符。請撥打 {phoneNumber} 與我們連絡，按 # 獲取援助以將您的記錄與您的連絡資訊進行匹配。 ",
                "ko" => $"귀하는 최근 주정부에 디지털 COVID-19 인증 기록을 요청하셨습니다. 유감스럽게도 귀하가 제공하신 정보는 저희 시스템상 정보와 일치하지 않습니다. {phoneNumber} 번으로 전화하여, # 버튼을 누르고 귀하의 기록과 연락처 정보 일치를 확인하는 데 도움을 받으시기 바랍니다.",
                "vi" => $"Gần đây bạn yêu cầu hồ sơ xác nhận COVID-19 kỹ thuật số từ tiểu bang. Rất tiếc, thông tin mà bạn cung cấp không khớp với thông tin có trong hệ thống của chúng tôi. Hãy liên hệ với chúng tôi theo số {phoneNumber}, nhấn # để được trợ giúp khớp thông tin hồ sơ với thông tin liên lạc của bạn.",
                "ar" => $"لقد قمت مؤخرًا بطلب الحصول على سجل التحقق الرقمي من فيروس كوفيد-19 من الولاية. ولكن للأسف، المعلومات التي قمت بتقديمها لا تتطابق مع المعلومات الموجودة على نظامنا. تواصل معنا على الرقم التالي {phoneNumber} واضغط على الرمز (#) للحصول على مساعدة في تحقيق التطابق بين سجلك ومعلومات التواصل الخاصة",
                "tl" => $"Kamakailan kang humiling ng digital na rekord sa pagberipika ng pagpapabakuna sa COVID-19 mula sa estado. Sa kasamaang-palad, hindi tumutugma ang ibinigay mong impormasyon sa impormasyong nasa system namin. Makipag-ugnayan sa amin sa {phoneNumber}, at pindutin ang # para sa tulong sa pagtugma ng iyong rekord sa iyong impormasyon sa pakikipag-ugnayan.",
                _ => $"You recently requested a digital COVID-19 verification record from the state. Unfortunately, the information you provided does not match information in our system. Contact us at {phoneNumber}, press # for help in matching your record to your contact information."
            };
        }

        public static string FormatNotFoundHtml(string lang, string webUrl, string contactUsUrl, string vaccineFAQUrl, string covidWebUrl, string emailLogoUrl)
        {
            if (String.IsNullOrEmpty(lang))
                throw new Exception("lang is null");

            if (String.IsNullOrEmpty(webUrl))
                throw new Exception("webUrl is null");

            if (String.IsNullOrEmpty(contactUsUrl))
                throw new Exception("contactUsUrl is null");

            if (String.IsNullOrEmpty(vaccineFAQUrl))
                throw new Exception("vaccineFAQUrl is null");

            if (String.IsNullOrEmpty(covidWebUrl))
                throw new Exception("covidWebUrl is null");

            if (String.IsNullOrEmpty(emailLogoUrl))
                throw new Exception("emailLogoUrl is null");

            return lang switch
            {
                "es" => $"<img src='{webUrl}/imgs/MyTurn-logo.png'><br/>" +
                    $"<h3 style='color: #f06724'>Registro digital de vacunación contra el COVID-19</h3>" +
                    $"<p>Hace poco solicitó un registro digital de vacunación contra el COVID-19 a <a href='{webUrl}'>MyVaccineRecord.CDPH.ca.gov</a>. Desafortunadamente, la información que proporcionó no coincide con la información que tenemos en el sistema. Puede <a href='{webUrl}'>enviar otra solicitud</a> con otro número de teléfono o dirección de correo electrónico, o puede comunicarse con el <a href='{contactUsUrl}'>asistente virtual para COVID-19 del CDPH</a> para obtener ayuda para hacer que su registro coincida con su información de contacto.</p><br/>" +
                    $"<p>Puede presentar otra solicitud en el sistema de <a href='{webUrl}'>registro digital de verificación de la COVID-19</a> con un número de teléfono o dirección de correo electrónico diferente; puede <a href='{contactUsUrl}'>comunicarse con nosotros</a> para que lo ayudemos a fin de que coincida la información de contacto con los registros; o bien, puede comunicarse con su proveedor para asegurarse de que la información ha sido enviada al sistema estatal.</p>" +
                    $"<p><b>¿Tiene preguntas?</b></p>" +
                    $"<p>Visite nuestra página de <a href='{vaccineFAQUrl}'>preguntas frecuentes</a> para obtener más información sobre el registro digital de verificación de la COVID-19.</p>" +
                    $"<p><b>Manténgase informado.</b></p>" +
                    $"<p><a href='{covidWebUrl}'>Consulte la información más reciente</a> sobre el COVID-19.</p><br/>" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Correo electrónico oficial del Departamento de Salud del Estado de Washington</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}'></p></footer>",
                "zh-CN" => $"<img src='{webUrl}/imgs/MyTurn-logo.png'><br/>" +
                    $"<h3 style='color: #f06724'>数字 COVID-19 验证记录</h3>" +
                    $"<p>您最近向 <a href='{webUrl}'>数字 COVID-19 验证记录系统</a> 请求过数字 COVID-19 验证记录。很遗憾，您提供的信息与州系统中的信息不符。</p><br/>" +
                    $"<p>您可以使用不同的手机号码或电子邮件地址在 <a href='{webUrl}'>数字 COVID-19 验证记录</a> 系统中提交另一个请求，您还可以 <a href='{contactUsUrl}'>联系我们</a> 寻求帮助，将您的记录与您的联系信息进行匹配，或者您可以联系您的医疗保健提供者以确保您的信息已提交至州系统。</p>" +
                    $"<p><b>仍有疑问？</b></p>" +
                    $"<p>请访问我们的常见问题解答 (<a href='{vaccineFAQUrl}'>FAQ</a>) 页面，以了解有关您的数字 COVID-19 验证记录的更多信息。</p>" +
                    $"<p><b>保持关注。</b></p>" +
                    $"<p><a href='{covidWebUrl}'>查看 COVID-19 最新信息</a>。</p><br/>" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health （华盛顿州卫生部）官方电子邮件</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}'></p></footer>",
                "zh-TW" => $"<img src='{webUrl}/imgs/MyTurn-logo.png'><br/>" +
                    $"<h3 style='color: #f06724'>數位 COVID-19 驗證記錄</h3>" +
                    $"<p>您最近向 <a href='{webUrl}'>數位 COVID-19 驗證記錄系統</a> 請求過數位 COVID-19 驗證記錄。很遺憾，您提供的資訊與州系統中的資訊不符。</p><br/>" +
                    $"<p>您可以使用不同的手機號碼或電子郵件地址在 <a href='{webUrl}'>數位 COVID-19 驗證記錄</a> 系統中提交另一個請求，您還可以 <a href='{contactUsUrl}'>與我們連絡</a> 尋求幫助，將您的記錄與您的連絡資訊進行匹配，或者您可以連絡您的醫療保健提供者以確保您的資訊已提交至州系統。</p>" +
                    $"<p><b>仍有疑問？</b></p>" +
                    $"<p>請造訪我們的常見問題解答 (<a href='{vaccineFAQUrl}'>FAQ</a>) 頁面，瞭解有關您的數位 COVID-19 驗證記錄的更多資訊。</p>" +
                    $"<p><b>保持關注。</b></p>" +
                    $"<p><a href='{covidWebUrl}'>檢視最新資訊</a>，與 COVID-19 密切相關的資訊。</p><br/>" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health （華盛頓州衛生部）官方電子郵件</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}'></p></footer>",
                "ko" => $"<img src='{webUrl}/imgs/MyTurn-logo.png'><br/>" +
                    $"<h3 style='color: #f06724'>디지털 COVID-19 인증 기록</h3>" +
                    $"<p>귀하는 최근 <a href='{webUrl}'>디지털 COVID-19 인증 기록 시스템</a> 에 디지털 COVID-19 인증 기록을 요청하셨습니다. 유감스럽게도 귀하가 제공하신 정보는 주정부 시스템상 정보와 일치하지 않습니다.</p><br/>" +
                    $"<p>다른 휴대전화 번호나 이메일 주소로 <a href='{webUrl}'>디지털 COVID-19 인증 기록 시스템</a> 에 별도의 요청을 제출하실 수 있습니다. <a href='{contactUsUrl}'>저희에게 연락</a> 하여 귀하의 기록을 연락처 정보와 일치시키는 데 도움을 받으시거나, 담당 의료서비스 제공자에게 문의하여 귀하의 정보가 주정부 시스템에 제출되었는지 확인하실 수 있습니다.</p>" +
                    $"<p><b>궁금한 사항이 있으신가요?</b></p>" +
                    $"<p>디지털 COVID-19 인증 기록에 대해 자세히 알아보려면 자주 묻는 질문 (<a href='{vaccineFAQUrl}'>FAQ서</a>) 페이지를 참조해 주십시오.</p>" +
                    $"<p><b>최신 정보를 확인하십시오.</b></p>" +
                    $"<p>COVID-19 관련 <a href='{covidWebUrl}'>최신 정보 보기</a></p><br/>" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health (워싱턴주 보건부) 공식 이메일</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}'></p></footer>",
                "vi" => $"<img src='{webUrl}/imgs/MyTurn-logo.png'><br/>" +
                    $"<h3 style='color: #f06724'>Hồ sơ Xác nhận COVID-19 kỹ thuật số</h3>" +
                    $"<p>Gần đây bạn yêu cầu Hồ sơ Xác nhận COVID-19 kỹ thuật số từ <a href='{webUrl}'>Mhệ thống Hồ sơ Xác nhận COVID-19 kỹ thuật số</a>. Rất tiếc, thông tin mà bạn cung cấp không khớp với thông tin có trong hệ thống của tiểu bang.</p><br/>" +
                    $"<p>Bạn có thể gửi yêu cầu khác trong hệ thống <a href='{webUrl}'>Hồ sơ Xác nhận COVID-19 kỹ thuật số</a> với một số điện thoại di động hoặc địa chỉ email khác, bạn có thể <a href='{contactUsUrl}'>liên hệ với chúng tôi</a> để được trợ giúp khớp thông tin hồ sơ với thông tin liên lạc của bạn, hoặc bạn có thể liên lạc với nhà cung cấp của mình để đảm bảo rằng thông tin của bạn đã được gửi đến hệ thống của tiểu bang.</p>" +
                    $"<p><b>Có câu hỏi?</b></p>" +
                    $"<p>Truy cập vào trang Câu Hỏi Thường Gặp <a href='{vaccineFAQUrl}'>FAQ</a> để tìm hiểu thêm về Hồ Sơ Xác nhận COVID-19 kỹ thuật số của bạn.</p>" +
                    $"<p><b>Luôn cập nhật thông tin.</b></p>" +
                    $"<p><a href='{covidWebUrl}'>Xem thông tin mới nhất</a> về COVID-19.</p><br/>" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Email chính thức của Washington State Department of Health (Sở Y Tế Tiểu Bang Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}'></p></footer>",
                "ar" => $"<img src='{webUrl}/imgs/MyTurn-logo.png' dir='rtl'><br/>" +
                    $"<h3 dir='rtl' style='color: #f06724'>سجل التحقق الرقمي من فيروس كوفيد-19</h3>" +
                    $"<p dir='rtl'>لقد قمت مؤخرًا بطلب الحصول على سجل التحقق الرقمي من فيروس <a href='{webUrl}'>كوفيد-19 من نظام سجل التحقق الرقمي من فيروس كوفيد-19</a> . ولكن للأسف، المعلومات التي قمت بتقديمها لا تتوافق مع المعلومات الموجودة على نظام الولاية.</p>" +
                    $"<p dir='rtl'>يمكنك التقدم بطلب آخر في نظام <a href='{webUrl}'>كوفيد-19 من نظام سجل التحقق الرقمي من فيروس كوفيد-19</a> باستخدام رقم هاتف محمول أو بريد إلكتروني مختلف، أو  يمكنك <a href='{contactUsUrl}'>التواصل معنا</a> للحصول على مساعدة في تحقيق التطابق بين سجلك ومعلومات التواصل الخاصة بك، أو يمكنك التواصل مع مُقدِّم الخدمة المعنّي بك للتأكد من إرسال معلوماتك إلى نظام الولاية.</p><br/>" +
                    $"<p dir='rtl'><b>هل لديك أي أسئلة؟ </b></p>" +
                    $"<p dir='rtl'>قم بزيارة صفحة <a href='{vaccineFAQUrl}'>الأسئلة</a> الشائعة  الخاصة بنا للاطلاع على مزيدٍ من المعلومات حول السجل الرقمي للقاح كوفيد-19 الخاص بك.</p>" +
                    $"<p dir='rtl'><b>ابقَ مطلعًا.</b></p>" +
                    $"<p dir='rtl'><a href='{covidWebUrl}'>عرض آخر المعلومات</a> عن فيروس كوفيد-19.</p>" +
                    $"<hr>" +
                    $"<footer><p dir='rtl' style='text-align:center'>البريد الإلكتروني الرسمي الخاص بـ Washington State Department of Health (إدارة الصحة في ولاية واشنطن)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}'></p></footer>",
                "tl" => $"<img src='{webUrl}/imgs/MyTurn-logo.png'><br/>" +
                    $"<h3 style='color: #f06724'>Digital na Rekord sa Pagberipika ng Pagpapabakuna sa COVID-19</h3>" +
                    $"<p>Kamakailan kang humiling ng Digital na Rekord sa Pagberipika ng Pagpapabakuna sa COVID-19 mula <a href='{webUrl}'>system ng Digital na Rekord sa Pagberipika ng Pagpapabakuna sa COVID-19</a>. Sa kasamaang-palad, hindi tumutugma ang ibinigay mong impormasyon sa impormasyong nasa system ng estado.</p><br/>" +
                    $"<p>Maaari kang magsumite sa isa pang kahilingan sa system ng <a href='{webUrl}'>Digital na Rekord sa Pagberipika ng Pagpapabakuna sa COVID-19</a> gamit ang ibang numero ng mobile na telepono o email address, <a href='{contactUsUrl}'>makipag-ugnayan sa amin</a> para sa tulong sa pagtugma ng iyong rekord sa impormasyon sa pakikipag-ugnayan mo, o makipag-ugnayan sa iyong provider para tiyaking isinumite sa system ng estado ang iyong impormasyon.</p>" +
                    $"<p><b>May mga tanong?</b></p>" +
                    $"<p>Bisitahin ang aming page ng Mga Madalas Itanong (<a href='{vaccineFAQUrl}'>FAQ</a>) para matuto pa tungkol sa iyong Digital na Rekord sa Pagberipika ng Pagpapabakuna sa COVID-19.</p>" +
                    $"<p><b>Manatiling May Kaalaman.</b></p>" +
                    $"<p><a href='{covidWebUrl}'>Tingnan ang pinakabagong impormasyon</a> tungkol sa COVID-19.</p><br/>" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Opisyal na Email ng Washington State Department of Health (Departamento ng Kalusugan ng Estado ng Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}'></p></footer>",
                _ => $"<img src='{webUrl}/imgs/MyTurn-logo.png'><br/>" +
                    $"<h3 style='color: #f06724'>Digital COVID-19 Verification Record</h3>" +
                    $"<p>You recently requested a Digital COVID-19 Verification Record from the <a href='{webUrl}'>Digital COVID-19 Verification Record system</a>. Unfortunately, the information you provided does not match information in the state system. " +
                    $"<p>You can submit another request in the <a href='{webUrl}'>Digital COVID-19 Verification Record system</a> with a different mobile phone number or email address, you can <a href='{contactUsUrl}'>contact us</a> for help in matching your record to your contact information, or you can contact your provider to ensure your information has been submitted to the state system.</p>" +
                    $"<p><b>Have questions?</b></p>" +
                    $"<p>Visit our Frequently Asked Questions <a href='{vaccineFAQUrl}'>(FAQ)</a> page to learn more about your Digital COVID-19 Verification Record.</p>" +
                    $"<p><b>Stay Informed.</b></p>" +
                    $"<p><a href='{covidWebUrl}'>View the latest information</a> on COVID-19.</p><br/>" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Official Washington State Department of Health e-mail</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}'></p></footer>"
            };
        }

        public static string UppercaseFirst(string s)
        {
            // Check for empty string.
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            //consider BILLY BOB
            s = s.ToLower().Trim();
            var tokens = s.Split(" ").ToList();
            var formattedName = "";
            tokens.RemoveAll(b => b.Length == 0);
            foreach (var token in tokens)
            {
                formattedName += char.ToUpper(token[0]) + token[1..] + " ";
            }
            formattedName = formattedName.Trim();
            return formattedName;
        }

        public static bool InPercentRange(int currentMessageCallCount, int percentToVA)
        {
            if (currentMessageCallCount % 100 < percentToVA)
            {
                return true;
            }
            return false;
        }

        public static string Sanitize(string text)
        {
            if (text == null)
            {
                text = "";
            }
            var neutralizedString = HttpUtility.UrlEncode(SecurityElement.Escape(text));
            neutralizedString = neutralizedString.Replace("\n", "  ").Replace("\r", "");
            return neutralizedString;
        }
    }
}