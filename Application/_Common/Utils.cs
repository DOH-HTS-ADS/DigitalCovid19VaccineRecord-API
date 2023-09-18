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
            { "207", "Moderna COVID-19 mRNA" },
            { "208", "Pfizer COVID-19 mRNA" },
            { "210", "AstraZeneca COVID-19 Vector-NR" },
            { "211", "Novavax COVID-19 Vector-NR" },
            { "212", "Janssen COVID-19 Vector-NR " },
            { "213", "Unspecified COVID-19" },
            { "217", "Pfizer COVID-19 mRNA" },
            { "218", "Pfizer COVID-19 mRNA" },
            { "219", "Pfizer COVID-19 mRNA" },
            { "500", "Unspecified COVID-19 (Non-US)" },
            { "501", "QazCovid-in COVID-19 (Non-US)" },
            { "502", "Bharat Covaxin IV COVID-19 (Non-US)" },
            { "503", "CoviVac COVID-19 (Non-US)" },
            { "504", "Sputnik Light COVID-19 (Non-US)" },
            { "505", "Sputnik V COVID-19 (Non-US)" },
            { "506", "CanSino COVID-19 (Non-US)" },
            { "507", "Anhui Zhifei COVID-19 (Non-US)" },
            { "508", "Jiangsu COVID-19 (Non-US)" },
            { "509", "EpiVacCorona COVID-19 (Non-US)" },
            { "510", "Sinopharm BIBP IV COVID-19 (Non-US)" },
            { "511", "CoronaVac Sinovac IV COVID-19 (Non-US)" },
            { "517", "Corbevax COVID-19 (Non-US)" },
            { "516", "KCONVAC COVID-19 (Non-US)" },
            { "515", "Medigen COVID-19 (Non-US)" },
            { "514", "ZyCoV-D COVID-19 (Non-US)" },
            { "513", "Zifivax COVID-19 (Non-US)" },
            { "512", "Covifenz COVID-19 (Non-US)" },
            { "228", "Moderna COVID-19 mRNA" },
            { "227", "Moderna COVID-19 mRNA" },
            { "221", "Moderna COVID-19 mRNA" },
            { "225", "Sanofi COVID-19 (Non-US)" },
            { "226", "Sanofi COVID-19 (Non-US)" },
            { "229", "Moderna COVID-19 mRNA Bivalent" },
            { "300", "Pfizer COVID-19 mRNA Bivalent" },
            { "301", "Pfizer COVID-19 mRNA Bivalent" },
            { "230", "Moderna COVID-19 mRNA Bivalent" },
            { "302", "Pfizer COVID-19 mRNA Bivalent" },
            { "308", "Pfizer COVID-19 mRNA" },
            { "309", "Pfizer COVID-19 mRNA"  },
            { "310", "Pfizer COVID-19 mRNA" },
            { "311", "Moderna COVID-19 mRNA" },
            { "312", "Moderna COVID-19 mRNA" },
            { "331", "Novavax COVID-19 subunit" }
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
                        message.SetSubject("Digital COVID-19 Verification Record");
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
                "es" => $"Gracias por visitar el sistema de Registro digital de verificación de COVID-19. El enlace para recuperar su verificación de COVID-19 es válido por {linkExpireHours} horas. Una vez que acceda y se guarde en su dispositivo, el código QR no vencerá.",
                "zh" => $"欢迎访问数字 COVID-19 验证记录系统。用于检索您 COVID-19 验证的链接在 {linkExpireHours} 小时内有效。在您获取到 QR 码并将其储存到您的设备后，此 QR 码将不会过期。",
                "zh-TW" => $"歡迎造訪數位 COVID-19 驗證記錄系統。用於檢索您的 COVID-19 驗證的連結在 {linkExpireHours} 小時內有效。一旦您存取 QR 代碼並將其儲存到您的裝置後，此 QR 代碼將不會過期。",
                "ko" => $"디지털 COVID-19 인증 기록 시스템을 방문해 주셔서 감사합니다. COVID-19 인증을 조회하는 링크는 {linkExpireHours} 시간 동안 유효합니다. 확인하고 기기에 저장하면 QR 코드는 만료되지 않습니다.",
                "vi" => $"Cảm ơn bạn đã truy cập vào hệ thống Hồ sơ Xác nhận COVID-19 kỹ thuật số. Đường liên kết để truy xuất thông tin xác nhận COVID-19 của bạn có hiệu lực trong vòng {linkExpireHours} giờ. Sau khi đã truy cập và lưu vào thiết bị của bạn, mã QR sẽ không hết hạn.",
                "ar" => $"شكرًا لك على زيارة نظام سجل التحقق الرقمي من فيروس كوفيد-19. يظل رابط الحصول على التحقق من فيروس كوفيد-19 الخاص بك صالحًا لمدة 24 ساعة. وب{linkExpireHours}رد الوصول إليه وحفظه على جهازك، لن تنتهي صلاحية رمز الاستجابة السريعة (QR).",
                "tl" => $"Salamat sa pagbisita sa system ng Digital na Talaan ng Pagberipika para sa COVID-19. May bisa ang link para makuha ang iyong pagberipika para sa COVID-19 sa loob ng {linkExpireHours} na oras. Kapag na-access at na-save na ito sa iyong device, hindi mawawalan ng bisa ang QR code.",
                "ru" => $"Благодарим вас за то, что воспользовались системой цифровых записей о вакцинации от COVID-19. Ссылка для получения подтверждения вакцинации от COVID-19 действительна в течение {linkExpireHours} часов. После перехода по ссылке и сохранения записи на вашем устройстве QR-код все еще будет действительным.",
                "ja" => $"COVID-19ワクチン接種電子記録システムをご利用いただきありがとうございます。COVID-19ワクチン接種記録を入手するためのリンクは {linkExpireHours} 時間有効です。リンクにアクセスし、お使いのデバイスにワクチン接種記録を保存すると、QRコードは無期限でご利用いただけます。",
                "fr" => $"Merci d'avoir consulté le système d'Attestation numérique de vaccination COVID-19. Le lien pour récupérer votre attestation de vaccination COVID-19 est valable pendant {linkExpireHours} heures. Une fois que vous l'avez enregistré sur votre appareil, le code QR n'expire pas.",
                "tr" => $"Dijital COVID-19 Doğrulama Kaydı sistemini ziyaret ettiğiniz için teşekkür ederiz. COVID-19 doğrulamanızı alacağınız bağlantı sadece {linkExpireHours} saat geçerlidir. Bir kez erişilip cihazınıza kaydedildikten sonra kare kodun süresi dolmaz.",
                "uk" => $"Дякуємо, що скористалися системою «Електронний запис про підтвердження вакцинації від COVID-19». Посилання для отримання запису про підтвердження вакцинації від COVID-19 дійсне протягом {linkExpireHours} годин. Після переходу за посиланням і збереження запису на вашому пристрої QR-код усе ще буде дійсним.",
                "ro" => $"Vă mulțumim că ați accesat sistemul pentru Certificat digital COVID-19. Linkul de unde puteți prelua certificatul COVID-19 este valabil timp de {linkExpireHours} de ore. După ce l-ați accesat și l-ați salvat în telefon, codul QR nu va expira.",
                "pt" => $"Obrigado por acessar o sistema do Comprovante digital de vacinação contra a COVID-19. O link para obter o seu comprovante de vacinação contra a COVID-19 é válido por {linkExpireHours} horas. Após acessar o código QR e salvá-lo em seu dispositivo, ele não expirará.",
                "hi" => $"डिजिटल COVID-19 वेरिफिकेशन रिकॉर्ड प्रणाली पर जाने के लिए धन्यवाद। आपके COVID-19 वेरिफिकेशन को पुनः प्राप्त करने का लिंक {linkExpireHours} घंटे के लिए वैध है। आपके डिवाइस पर उपयोग करने और सहेजने के बाद, QR कोड की समय-सीमा समाप्त नहीं होगी।",
                "de" => $"Danke für Ihren Besuch beim COVID-19-Digitalzertifikat-System. Der Link zum Abrufen Ihres COVID-19-Zertifikats ist {linkExpireHours} Stunden lang gültig. Nachdem Sie den QR-Code aufgerufen und auf Ihrem Gerät gespeichert haben, läuft der Code nicht ab.",
                "ti" => $"ን ዲጂታላዊ ናይ ኮቪድ-19 መረጋገጺ መዝገብ ብምብጻሕኩም ነመስግነኩም። ናይ ኮቪድ-19 መረጋገጺ መርከቢ ሊንክ ድማ ን {linkExpireHours} ሰዓታት ቅቡል እዩ። ሓንሳብ ምስ ኣተኹምን ኣብ መሳርሒትኩም ምስ ተዓቀበን ድማ፡ እቲ ናይ QR code ዕለቱ ኣይወድቕን እዩ።",
                "te" => $"డిజిటల్ కొవిడ్-19 ధృవీకరణ రికార్డ్ సిస్టమ్​ని సందర్శించినందుకు మీకు ధన్యవాదాలు. మీ కొవిడ్-19 ధృవీకరణను తిరిగి పొందే లింక్ {linkExpireHours} గంటలపాటు చెల్లుబాటు అవుతుంది. మీరు యాక్సెస్ చేసుకొని, మీ పరికరంలో సేవ్ చేసిన తరువాత, QR కోడ్ గడువు తీరదు.",
                "sw" => $"Asante kwa kutembelea mfumo wa Rekodi ya Kidijitali ya Uthibitishaji wa COVID-19. Kiungo cha kupata uthibitishaji wako wa COVID-19 kitakuwa amilifu kwa saa {linkExpireHours}. Mara tu imefikiwa na kuhifadhiwa kwenye kifaa chako, msimbo wa QR hautaisha muda.",
                "so" => $"Waad ku mahadsan tahay soo booqashadaada nidaamka Diiwaanka Xaqiijinta Tallaalka COVID-19 ee Dhijitaalka ah. Lifaaqa aad kula soo baxeyso xaqiijintaada tallaalka COVID-19 ayaa ansax ah oo shaqeynayo {linkExpireHours} saacadood. Marki la galo oo lagu keydiyo taleefankaaga, koodhka jawaabta degdegga ma dhacayo. Arag Diiwaanka Tallaalka ",
                "sm" => $"Faafetai mo le asiasi mai i faamaumauga faamaonia o le KOVITI-19. O le fesootaiga e maua ai faamaumauga o le KOVITI-19 e {linkExpireHours} itula lona aoga. A maua uma faamatalaga, ia faamauina, lelei ma sefe i lau masini, o le QR code e mafai ona faaogaina e le toe muta.",
                "pa" => $"ਡਿਜੀਟਲ ਕੋਵਿਡ-19 ਵੇਰਿਫਿਕੇਸ਼ਨ ਰਿਕਾਰਡ ਸਿਸਟਮ 'ਤੇ ਆਉਣ ਲਈ ਤੁਹਾਡਾ ਧੰਨਵਾਦ। ਤੁਹਾਡੀ ਕੋਵਿਡ-19 ਵੇਰਿਫਿਕੇਸ਼ਨ ਨੂੰ ਮੁੜ ਪ੍ਰਾਪਤ ਕਰਨ ਲਈ ਲਿੰਕ {linkExpireHours} ਘੰਟਿਆਂ ਲਈ ਵੈਧ ਹੈ। ਇੱਕ ਵਾਰ ਤੁਹਾਡੇ ਡਿਵਾਈਸ 'ਤੇ ਐਕਸੈਸ ਕਰਨ ਅਤੇ ਸੁਰੱਖਿਅਤ ਹੋਣ ਤੋਂ ਬਾਅਦ, QR ਕੋਡ ਦੀ ਮਿਆਦ ਖ਼ਤਮ ਨਹੀਂ ਹੋਵੇਗੀ। ਵੈਕਸੀਨ ਰਿਕਾਰਡ ਵੇਖੋ ",
                "ps" => $"د ډیجیټل COVID-19 تائید ثبت سیسټم لیدو لپاره ستاسو مننه. ستاسو د COVID-19 تائید ترلاسه کولو لینک د {linkExpireHours} ساعتونو لپاره اعتبار لري. یوځل چې ستاسو وسیلې ته لاسرسی ولري او خوندي شي، نو د QR کوډ به پای ته ونه رسیږي. ",
                "ur" => $"ڈیجیٹل کووڈ-19 تصدیقی ریکارڈ کا سسٹم ملاحظہ کرنے کا شکریہ۔ کووڈ-19 کی تصدیق وصول کرنے کا لنک {linkExpireHours} گھنٹے تک قابل استعمال ہو گا۔ رسائی لے کر ڈیوائس میں محفوظ کرنے کے بعد کیو آر کوڈ کی میعاد ختم نہیں ہو گی۔",
                "ne" => $"डिजिटल कोभिड-19 प्रमाणीकरण रेकर्ड प्रणाली हेर्नुभएको धन्यवाद। तपाईंको कोभिड-19 प्रमाणीकरण पुनः प्राप्ति गर्ने लिङ्क {linkExpireHours} घण्टाकोसम्म मान्य हुन्छ। पहुँच गरेर तपाईंको यन्त्रमा बचत भइसकेपछि, QR कोडको म्याद समाप्त हुने छैन।",
                "mxb" => $"Kuta’avi sa ja ni nde’e ní Tutu nuu Kaa ndichí siki Tu’un Nasa iyo ní jín kue’e COVID-19. Enlace tágua nani’in ní tu’un siki nasa iyo ni nuu kue’e COVID-19 tiñu ji nuu {linkExpireHours} hora. Tú ja ni kivi ní de ni tava’a ní nuu kaa ndichí ní, código QR ma koo tiñu ji. ",
                "mh" => $"Kommol n̄an am itok n̄an system in Rekoot in Kein Kamool COVID-19 eo am ilo online. Link eo kwar bōke n̄an kein kamool COVID-19 eo am ej jerbal n̄an {linkExpireHours} awa. QR code eo eban jemlọk n̄e kwar bōke im kakwone ilo telebon eo am.",
                "mam" => $"Chjontay ma tz’oktz tq’olb’e’na jqeya qkloj Tqanil toj Yolb’il tun Tjyet COVID-19toj jxk’utz’ib’. Ja enlace te tu’ ttiq’ay jte cheylakxta tej tx’u’j yab’ilo te COVID-19 b’a’x teja toj jun q’ij b’ix jun qoniya mo toj {linkExpireHours} amb’il. Juxmaj uj ot tz’okxay ex ot ku’x tk’u’na toj jtey txk’utz’ib’, j-código QR ya mi kub’ najt.",
                "lo" => $"ຂອບໃຈທີ່ເຂົ້າເບິ່ງລະບົບບັນທຶກການຢັ້ງຢືນ ພະຍາດ ໂຄວິດ-19 ແບບດີຈີຕອນ. ລິ້ງເພື່ອຮຽກຄົ້ນຂໍ້ມູນການຢັ້ງຢືນ ພະຍາດ ໂຄວິດ-19 ຂອງທ່ານແມ່ນໃຊ້ໄດ້ ພາຍໃນເວລາ {linkExpireHours} ຊົ່ວໂມງ. ເມື່ອເຂົ້າເຖິງ ແລະ ບັນທຶກໄວ້ໃນອຸປະກອນຂອງທ່ານແລ້ວ, ລະຫັດ QR ຈະບໍ່ໝົດອາຍຸ.",
                "km" => $"អរគុណ​សម្រាប់​ការ​ចូល​​មកកាន់​ប្រព័ន្ធ​កំណត់ត្រា​ផ្ទៀងផ្ទាត់​ជំងឹ​ COVID-19 ជាទម្រង់​ឌីជីថល។​ តំណភ្ជាប់ដើម្បី​ទទួលបាន​មកវិញ​នូវ​ការផ្ទៀងផ្ទាត់​ជំងឺ​ COVID-19 របស់អ្នក​ គឺ​មានសុពលភាព​រយៈពេល​ {linkExpireHours}ម៉ោង​។ កូដ QR នឹង​មិនផុត​សុពលភាព​ទេ​ នៅពេលបានចូលប្រើប្រាស់​និង​រក្សាទុក​ក្នុង​ឧបករណ៍​របស់អ្នក​។",
                "kar" => $"တၢ်ဘျုးလၢနကွၢ် ဒံးကၠံၣ်တၢၣ်(လ) COVID-19 တၢ်အုၣ်သးတၢ်မၤနီၣ်မၤဃါ တၢ်မၤ  အကျဲသနူ. ပှာ်ဘျးစဲလၢနကဃုမၤန့ၢ်က့ၤန COVID-19 တၢ်အုၣ်သးအံၤဖိးသဲစးလၢ {linkExpireHours} အဂီၢ်လီၤ. ဖဲနနုာ်လီၤမၤန့ၢ်ဒီးပာ်ကီၤဃာ်တၢ်လၢနပီးလီပူၤတစုန့ၣ်, QR နီၣ်ဂံၢ်အဆၢကတီၢ် တလၢာ်ကွံာ်ဘၣ်. ",
                "fj" => $"Vinaka vakalevu na nomu sikova mai na iVolatukutuku Vakalivaliva ni iVakadinadina ni veika e Vauca na COVID-19. Na isema mo rawa ni raica tale kina na na ivakadinadina ni veika e vauca na COVID-19 me baleti iko ena rawa ni vakayagataki ga ena loma ni {linkExpireHours} na aua. Ni sa laurai oti qai maroroi ina nomu kompiuta se talevoni, ena rawa ni vakayagataki tiko ga na QR code.",
                "fa" => $"بابت بازدید از سیستم «نسخه دیجیتال گواهی واکسیناسیون COVID-19»، از شما متشکریم. پیوند بازیابی گواهی واکسیناسیون COVID-19 به‌مدت {linkExpireHours} ساعت معتبر است. به‌محض اینکه کد QR را دریافت و آن را در دستگاهتان ذخیره کنید، این کد دیگر منقضی نمی‌شود.",
                "prs" => $"تشکر برای بازدید از سیستم سابقه دیجیتل تصدیق کووید-19. لینک دریافت مجدد تصدیق کووید-19 تا {linkExpireHours} ساعت معتبر است. زمانی که دسترسی پیدا کرده و در دستگاه شما ذخیره گردید، کد پاسخ سریع منقضی نخواهد شد.",
                "chk" => $"Kinisou ren om tota won ewe Digital COVID-19 Afatan Record System. Ewe link ika anen kopwe feino ngeni ren om kopwe angei noum taropwen COVID-19 mi eoch non ukukun {linkExpireHours} awa. Ika pwe ka fen tonong ika a nom porausen noum we fon ika kamputer, ewe QR code esap muchuno manamanin.",
                "my" => $"ဒီဂျစ်တယ်လ် ကိုဗစ်-19 အတည်ပြုချက် မှတ်တမ်း စနစ် ကို ဝင်လေ့လာသည့်အတွက် ကျေးဇူးတင်ပါသည်။ သင့် ကိုဗစ်-19 အတည်ပြုချက် ကို ထုတ်ယူရန် လင့်ခ်မှာ {linkExpireHours} နာရီကြာ သက်တမ်းရှိပါသည်။ ဝင်သုံးပြီး သင့်ကိရိယာထဲတွင် သိမ်းဆည်းထားလျှင် ကျူအာ ကုဒ်သည် သက်တမ်းကုန်ဆုံးသွားလိမ့်မည် မဟုတ်ပါ။",
                "am" => $"የዲጂታል COVID-19 ማረጋገጫ መዝገብ ስርዓትን ስለጎበኙ እናመሰግናለን። የእርስዎን የ COVID-19 ማረጋገጫ መልሶ ማውጫ ሊንክ ለ {linkExpireHours} ሰዓታት ያገለግላል። አንዴ አግኝተውት ወደ መሳሪያዎ ካስቀመጡት፣ የ QR ኮድዎ ጊዜው አያበቃም። ",
                "om" => $"Mala Mirkaneessa Ragaa Dijitaalaa COVID-19 ilaaluu keessaniif galatoomaa. Liinkin mirkaneessa COVID-19 keessan deebisanii argachuuf yookin seevii gochuuf gargaaru sa’aatii {linkExpireHours}’f hojjata. Meeshaa itti fayyadamtan (device) irratti argachuun danda’amee erga seevii ta’een booda, koodin QR yeroon isaa irra hin darbu.",
                "to" => $"Mālō ho’o ‘a’ahi mai ki he fa’unga ma’u’anga Digital COVID-19 Verification Record system (Lēkooti Fakamo’oni ki he Huhu Malu’i COVID-19). Ko e link ke ma’u ho’o fakamo’oni huhu malu’i COVID-19 ‘e ‘aonga pe ‘i he houa ‘e {linkExpireHours}. Ko ho’o ma’u pē mo tauhi ki ho’o me’angāué, he’ikai toe ta’e’aonga ‘a e QR code.",
                "ta" => $"மின்னணு கொவிட்-19 சரிபார்ப்புப் பதிவு முறையைப் பார்வையிட்டதற்கு நன்றி. உங்கள் கொவிட்-19 சரிபார்ப்பைப் பெறுவதற்கான இணைப்பு {linkExpireHours} மணிநேரத்திற்கு செல்லுபடியாகும். இணைப்பை அணுகி உங்கள் சாதனத்தில் சேமித்துவிட்டால், QR குறியீடு காலாவதியாகாது.",
                "hmn" => $"Ua tsaug rau kev mus saib kev ua hauj lwm rau Kev Txheeb Xyuas Ntaub Ntawv Sau Tseg Txog Kab Mob COVID-19 Ua Dis Cis Tauj. Txoj kab txuas nkag mus txhawm rau rub koj li kev txheeb xyuas kab mob COVID-19 yog siv tau li {linkExpireHours} xuab moos. Thaum tau nkag mus thiab tau muab kaw cia rau koj lub xov tooj lawm, tus khauj QR yuav tsis paub tag sij hawm lawm.",
                "th" => $"ขอขอบคุณที่เยี่ยมชมระบบบันทึกการยืนยันเกี่ยวกับโควิด-19 แบบดิจิทัล ลิงก์การเรียกดูข้อมูลการยืนยันเกี่ยวกับโควิด-19 ของคุณมีอายุ {linkExpireHours} ชั่วโมง เมื่อคุณได้เข้าถึงและบันทึกลงในอุปกรณ์ของคุณแล้ว คิวอาร์โค้ดจะไม่หมดอายุ ",
                _ => $"Thank you for visiting the Digital COVID-19 Verification Record system. The link to retrieve your COVID-19 verification is valid for {linkExpireHours} hours. Once accessed and saved to your device, the QR code will not expire.",
            };
        }

        public static string FormatSms2(string url, string lang)
        {
            return lang switch
            {
                "es" => $"{url}",
                "zh" => $"{url}",
                "zh-TW" => $"{url}",
                "ko" => $"{url}",
                "vi" => $"{url}",
                "ar" => $"{url}",
                "tl" => $"{url}",
                "ru" => $"{url}",
                "ja" => $"{url}",
                "fr" => $"{url}",
                "tr" => $"{url}",
                "uk" => $"{url}",
                "ro" => $"{url}",
                "pt" => $"{url}",
                "hi" => $"{url}",
                "de" => $"{url}",
                "ti" => $"{url}",
                "te" => $"{url}",
                "sw" => $"{url}",
                "so" => $"{url}",
                "sm" => $"{url}",
                "pa" => $"{url}",
                "ps" => $"{url}",
                "ur" => $"{url}",
                "ne" => $"{url}",
                "mxb" => $"{url}",
                "mh" => $"{url}",
                "mam" => $"{url}",
                "lo" => $"{url}",
                "km" => $"{url}",
                "kar" => $"{url}",
                "fj" => $"{url}",
                "fa" => $"{url}",
                "prs" => $"{url}",
                "chk" => $"{url}",
                "my" => $"{url}",
                "am" => $"{url}",
                "om" => $"{url}",
                "to" => $"{url}",
                "ta" => $"{url}",
                "hmn" => $"{url}",
                "th" => $"{url}",
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
                "es" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Registro digital de verificación de COVID-19</h1>" +
                    $"<p>Gracias por visitar el sistema de Registro digital de verificación de COVID-19. El enlace para recuperar su código de registro de vacunación de COVID-19 es válido por {linkExpireHours} horas. Una vez que acceda y se guarde en su dispositivo, el código QR no vencerá.</p>" +
                    $"<p><a href='{url}'>Consulte los registros de vacunación</a></p>" +
                    $"<p>Obtenga más información sobre cómo <a href='{cdcUrl}'>protegerse usted y proteger a otros</a> de los Centros para el Control y la Prevención de Enfermedades.</p>" +
                    $"<h2>¿Tiene alguna pregunta?</h2>" +
                    $"<p>Visite nuestra página de <a href='{vaccineFAQUrl}'>preguntas frecuentes</a> para obtener más información sobre el Registro digital de vacunación contra la COVID-19.</p>" +
                    $"<h2>Manténgase informado.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Consulte la información más reciente</a> sobre la COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Correo electrónico oficial del Departamento de Salud del Estado de Washington</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "zh" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>数字 COVID-19 验证记录</h1>" +
                    $"<p>欢迎访问数字 COVID-19 验证记录系统。用于检索您 COVID-19 疫苗记录码的链接在 {linkExpireHours} 小时内有效。在您获取到 QR 码并将其储存到您的设备后，此 QR 码将不会过期。</p>" +
                    $"<p><a href='{url}'>查看疫苗记录</a></p>" +
                    $"<p>从 Centers for Disease Control and Prevention（CDC，疾病控制与预防中心）了解更多关于如何<a href='{cdcUrl}'>保护自己和他人</a> 的相关信息。</p>" +
                    $"<h2>仍有疑问？</h2>" +
                    $"<p>请访问我们的<a href='{vaccineFAQUrl}'>常见问题解答 (FAQ)</a> 页面，以了解有关您的数字 COVID-19 疫苗记录的更多信息。</p>" +
                    $"<h2>保持关注。</h2>" +
                    $"<p><a href='{covidWebUrl}'>查看 COVID-19 最新信息</a>。</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health (华盛顿州卫生部）官方电子邮件</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "zh-TW" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>數位 COVID-19 驗證記錄</h1>" +
                    $"<p>歡迎造訪數位 COVID-19 驗證記錄系統。用於檢索您的 COVID-19 疫苗記錄碼的連結在 {linkExpireHours} 小時內有效。一旦您存取 QR 代碼並將其儲存到您的裝置後，此 QR 代碼將不會過期。</p>" +
                    $"<p><a href='{url}'>檢視疫苗記錄</a></p>" +
                    $"<p>從 Centers for Disease Control and Prevention（CDC，疾病控制與預防中心）瞭解更多關於如何<a href='{cdcUrl}'>保護自己和他人</a> 的相關資訊。</p>" +
                    $"<h2>仍有疑問？</h2>" +
                    $"<p>請造訪我們的<a href='{vaccineFAQUrl}'>常見問題解答 (FAQ)</a>頁面，以瞭解有關您的數位 COVID-19 疫苗記錄的更多資訊。</p>" +
                    $"<h2>保持關注。</h2>" +
                    $"<p><a href='{covidWebUrl}'>檢視 COVID-19 最新資訊</a>。</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health （華盛頓州衛生部）官方電子郵件 </p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ko" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>디지털 COVID-19 인증 기록</h1>" +
                    $"<p>디지털 COVID-19 인증 기록 시스템을 방문해 주셔서 감사합니다. COVID-19 백신 기록 코드를 조회하는 링크는 {linkExpireHours}시간 동안 유효합니다. 확인하고 기기에 저장하면 QR 코드는 만료되지 않습니다. </p>" +
                    $"<p><a href='{url}'>백신 기록 보기</a></p>" +
                    $"<p>Centers for Disease Control and Prevention(질병통제예방센터)에서 <a href='{cdcUrl}'>나와 타인을 보호</a> 하는 방법에 대해 자세히 확인해 보십시오.</p>" +
                    $"<h2>궁금한 사항이 있으신가요?</h2>" +
                    $"<p>디지털 COVID-19 백신 기록에 대해 자세히 알아보려면 <a href='{vaccineFAQUrl}'>자주 묻는 질문(FAQ)</a> 페이지를 참조해 주십시오.</p>" +
                    $"<h2>최신 정보를 확인하십시오.</h2>" +
                    $"<p>COVID-19 관련 <a href='{covidWebUrl}'>최신 정보 보기</a></p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health (워싱턴주 보건부) 공식 이메일</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "vi" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Hồ sơ Xác nhận COVID-19 kỹ thuật số</h1>" +
                    $"<p>Cảm ơn bạn đã truy cập vào hệ thống Hồ sơ Xác nhận COVID-19 kỹ thuật số. Đường liên kết để truy xuất mã hồ sơ vắc-xin COVID-19 của bạn có hiệu lực trong vòng {linkExpireHours} giờ. Sau khi đã truy cập và lưu vào thiết bị của bạn, mã QR sẽ không hết hạn.</p>" +
                    $"<p><a href='{url}'>Xem Hồ sơ Vắc-xin</a></p>" +
                    $"<p>Tìm hiểu thêm về cách <a href='{cdcUrl}'>tự bảo vệ mình và bảo vệ người khác</a> từ Centers for Disease Control and Prevention (CDC, Trung Tâm Kiểm Soát và Phòng Ngừa Dịch Bệnh).</p>" +
                    $"<h2>Bạn có câu hỏi?</h2>" +
                    $"<p>Truy cập vào trang <a href='{vaccineFAQUrl}'>Các Câu Hỏi Thường Gặp (FAQ)</a> để tìm hiểu thêm về Hồ Sơ Vắc-xin COVID-19 kỹ thuật số của bạn.</p>" +
                    $"<h2>Luôn cập nhật thông tin.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Xem thông tin mới nhất</a> về COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Email chính thức của Washington State Department of Health (Sở Y Tế Tiểu Bang Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ar" => $"<img dir='rtl' src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 dir='rtl' style='color: #C84C0E'>سجل التحقق الرقمي من فيروس كوفيد-19</h1>" +
                    $"<p dir='rtl'>شكرًا لك على زيارة نظام سجل التحقق الرقمي من فيروس كوفيد-19. يظل رابط الحصول على رمز سجل لقاح فيروس كوفيد-19 صالحًا لمدة {linkExpireHours} ساعة. وبمجرد الوصول إليه وحفظه على جهازك، لن تنتهي صلاحية رمز الاستجابة السريعة (QR).</p>" +
                    $"<p dir='rtl'><a href='{url}'>عرض سجل اللقاح</a> (متوفر باللغة الإنجليزية فقط)</p>" +
                    $"<p dir='rtl'>تعرَّف على مزيدٍ من المعلومات عن كيفية ح<a href='{cdcUrl}'>ماية نفسك والآخرين</a> (متوفر باللغة الإنجليزية فقط) من خلال Centers for Disease Control and Prevention ( CDC، مراكز مكافحة الأمراض والوقاية منها).</p>" +
                    $"<h2 dir='rtl'>هل لديك أي أسئلة؟</h2>" +
                    $"<p dir='rtl'>قم بزيارة صفحة <a href='{vaccineFAQUrl}'>الأسئلة الشائعة</a> (متوفر باللغة الإنجليزية فقط) الخاصة بنا للاطلاع على مزيدٍ من المعلومات حول السجل الرقمي للقاح كوفيد-19 الخاص بك</p>" +
                    $"<h2 dir='rtl'>ابقَ مطلعًا.</h2>" +
                    $"<p dir='rtl'>ع<a href='{covidWebUrl}'>رض آخر المعلومات</a> (متوفر باللغة الإنجليزية فقط) عن فيروس كوفيد-19.</p>" +
                    $"<hr>" +
                    $"<footer><p dir='rtl' style='text-align:center'>البريد الإلكتروني الرسمي الخاص بـ Washington State Department of Health (إدارة الصحة في ولاية واشنطن)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "tl" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Digital na Rekord sa Pagberipika ng Pagpapabakuna sa COVID-19</h1>" +
                    $"<p>Salamat sa pagbisita sa system ng Digital na Rekord sa Pagberipika ng Pagpapabakuna sa COVID-19. May bisa ang link para makuha ang iyong code sa pagberipika ng bakuna sa COVID-19 nang {linkExpireHours} na oras. Kapag na-aaccess at na-save na ito sa iyong device, hindi mag-e-expire ang QR code.</p>" +
                    $"<p><a href='{url}'>Tingnan ang Rekord ng Bakuna</a></p>" +
                    $"<p>Matuto pa tungkol sa kung paano <a href='{cdcUrl}'>protektahan ang iyong sarili at ang ibang tao</a> mula sa impormasyon mula sa Centers for Disease Control and Prevention (Mga Sentro sa Pagkontrol at Pag-iwas sa Sakit).</p>" +
                    $"<h2>May mga tanong?</h2>" +
                    $"<p>Bisitahin ang aming page ng <a href='{vaccineFAQUrl}'>Mga Madalas Itanong (FAQ)</a> para matuto pa tungkol sa iyong Digital na Rekord ng Bakuna sa COVID-19.</p>" +
                    $"<h2>Manatiling May Kaalaman.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Tingnan ang pinakabagong impormasyon</a> tungkol sa COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Opisyal na Email ng Washington State Department of Health (Departamento ng Kalusugan ng Estado ng Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ru" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Цифровая запись о вакцинации от COVID-19 </h1>" +
                    $"<p>Благодарим вас за то, что воспользовались системой цифровых записей о вакцинации от COVID-19. Ссылка на код для получения записи о вакцинации от COVID-19 действительна в течение {linkExpireHours} часов. После перехода по ссылке и сохранения записи на вашем устройстве QR-код все еще будет действительным.</p>" +
                    $"<p><a href='{url}'>Посмотреть запись о вакцинации</a></p>" +
                    $"<p>Узнайте больше о том, как <a href='{cdcUrl}'>защитить себя и других</a> согласно рекомендациям Centers for Disease Control and Prevention (Центры по контролю и профилактике заболеваний).</p>" +
                    $"<h2>Возникли вопросы?</h2>" +
                    $"<p>Чтобы узнать больше о цифровой записи о вакцинации от COVID-19, перейдите на нашу страницу <a href='{vaccineFAQUrl}'>«Часто задаваемые вопросы» (FAQ)</a>.</p>" +
                    $"<h2>Оставайтесь в курсе.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Получайте актуальную информацию</a> о COVID-19 .</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Официальный адрес электронной почты Washington State Department of Health (Департамент здравоохранения штата Вашингтон)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ja" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>COVID-19ワクチン接種電子記録</h1>" +
                    $"<p>COVID-19ワクチン接種電子記録システムをご利用いただきありがとうございます。COVID-19ワクチン接種記録を取得するためのリンクは {linkExpireHours} 時間有効です。リンクにアクセスし、お使 いのデバイスにワクチン接種記録を保存すると、QRコードは無期限でご利用いただけます。</p>" +
                    $"<p><a href='{url}'>ワクチン接種記録を表示</a></p>" +
                    $"<p>Centers for Disease Control and Prevention（CDC、疾病管理予防センター）の<a href='{cdcUrl}'>自分自身と他の人を保護する方法</a>で詳細をご覧ください。</p>" +
                    $"<h2>何か質問はありますか？</h2>" +
                    $"<p>COVID-19ワクチン接種電子記録についての詳細は、<a href='{vaccineFAQUrl}'>よくある質問（FAQ)</a>ページをご覧ください。</p>" +
                    $"<h2>最新の情報を入手する </h2>" +
                    $"<p>新型コロナ感染症について<a href='{covidWebUrl}'>最新情報を見る</a>。</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health（ワシントン州保健局）の公式電子メール</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "fr" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Attestation numérique de vaccination COVID-19</h1>" +
                    $"<p>Merci d'avoir consulté le système d'Attestation numérique de vaccination COVID-19. Le lien pour récupérer le code QR de votre attestation de vaccination COVID-19 est valable pendant {linkExpireHours} heures. Une fois que vous l'avez enregistré sur votre appareil, le code QR n'expire pas. </p>" +
                    $"<p><a href='{url}'>Afficher l'attestation de vaccination</a></p>" +
                    $"<p>Pour en savoir plus sur la façon de <a href='{cdcUrl}'>vous protéger et protéger les autres</a>, consultez le site Internet des Centers for Disease Control and Prevention (centres de contrôle et de prévention des maladies).</p>" +
                    $"<h2>Vous avez des questions?</h2>" +
                    $"<p>Consultez notre <a href='{vaccineFAQUrl}'>Foire Aux Questions (FAQ)</a> pour en savoir plus sur votre Attestation numérique de vaccination COVID-19.</p>" +
                    $"<h2>Informez-vous.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Voir les dernières informations</a> à propos du COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>E-mail officiel du Washington State Department of Health (ministère de la Santé de l'État de Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "tr" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                   $"<h1 style='color: #C84C0E'>Dijital COVID-19 Doğrulama Kaydı</h1>" +
                   $"<p>Dijital COVID-19 Doğrulama Kaydı sistemini ziyaret ettiğiniz için teşekkür ederiz. COVID-19 aşı kaydı kodunuzu alacağınız bağlantı sadece {linkExpireHours} saat geçerlidir. Bir kez erişilip cihazınıza kaydedildikten sonra kare kodun süresi dolmaz.</p>" +
                   $"<p><a href='{url}'>Aşı Kaydını Görüntüleyin</a></p>" +
                   $"<p><a href='{cdcUrl}'>Kendinizi ve sevdiklerinizi nasıl koruyacağınızı</a> Centers for Disease Control and Prevention (CDC, Hastalık Kontrol ve Korunma Merkezleri)'nden öğrenebilirsiniz.</p>" +
                   $"<h2>Sorularınız mı var?</h2>" +
                   $"<p>Dijital COVID-19 Aşı Kaydınız hakkında daha fazla bilgi almak için <a href='{vaccineFAQUrl}'>Sıkça Sorulan Sorular (SSS)</a> bölümümüzü ziyaret edin.</p>" +
                   $"<h2>Güncel bilgilere sahip olun.</h2>" +
                   $"<p>COVID-19 <a href='{covidWebUrl}'>hakkında en güncel bilgileri görüntüleyin</a>.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                   $"<hr>" +
                   $"<footer><p style='text-align:center'>Resmi Washington State Department of Health (Washington Eyaleti Sağlık Bakanlığı) E-postası</p>" +
                   $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "uk" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Електронний запис про підтвердження вакцинації від COVID-19</h1>" +
                    $"<p>Дякуємо, що скористалися системою «Електронний запис про підтвердження вакцинації від COVID-19». Посилання на код для отримання запису про підтвердження вакцинації від COVID-19 дійсне протягом {linkExpireHours} годин. Після переходу за посиланням і збереження запису на вашому пристрої QR-код усе ще буде дійсним.</p>" +
                    $"<p><a href='{url}'>Переглянути запис про статус вакцинації</a></p>" +
                    $"<p>Дізнайтеся більше про те, як <a href='{cdcUrl}'>захистити себе та інших</a>, ознайомившись із рекомендаціями Centers for Disease Control and Prevention (Центрів із контролю та профілактики захворювань у США).</p>" +
                    $"<h2>Маєте запитання?</h2>" +
                    $"<p>Щоб дізнатися більше про ваш електронний запис про підтвердження вакцинації від COVID-19, перегляньте розділ <a href='{vaccineFAQUrl}'>Найпоширеніші запитання</a>.</p>" +
                    $"<h2>Будьте в курсі.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Перегляньте найновішу інформацію</a> про COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Офіційна електронна адреса Washington State Department of Health (Департаменту охорони здоров’я штату Вашингтон)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ro" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Certificatul digital COVID-19</h1>" +
                    $"<p>Vă mulțumim că ați accesat sistemul de certificate digitale COVID-19. Linkul de unde puteți prelua fișa de vaccinare COVID-19 este valabil timp de {linkExpireHours} de ore. După ce l-ați accesat și l-ați salvat în telefon, codul QR nu va expira.</p>" +
                    $"<p><a href='{url}'>Vizualizați fișa de vaccinare</a></p>" +
                    $"<p>Aflați mai multe despre cum să <a href='{cdcUrl}'>vă protejați pe dvs. și pe ceilalți</a> de la Centers for Disease Control and Prevention (Centrele pentru controlul și prevenirea bolilor).</p>" +
                    $"<h2>Aveți întrebări?</h2>" +
                    $"<p>Accesați pagina Întrebări frecvente (<a href='{vaccineFAQUrl}'>Întrebări frecvente</a>) pentru a afla mai multe despre certificatul digital COVID-19.</p>" +
                    $"<h2>Rămâneți informat.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Vizualizați cele mai recente informații</a> referitoare la COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<footer><p style='text-align:center'>Adresa de e-mail oficială a Washington State Department of Health (Departamentului de Sănătate al Statului Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "pt" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Comprovante digital de vacinação contra a COVID-19</h1>" +
                    $"<p>Obrigado por visitar o sistema do Comprovante digital de vacinação contra a COVID-19. O link para recuperar o seu código de acesso ao comprovante de vacinação contra a COVID-19 é válido por {linkExpireHours} horas. Após acessar o código QR e salvá-lo em seu dispositivo, ele não expirará.</p>" +
                    $"<p><a href='{url}'>Ver o comprovante de vacinação</a></p>" +
                    $"<p>Saiba mais sobre como <a href='{cdcUrl}'>proteger a si mesmo e aos outros</a> com o Centers for Disease Control and Prevention (CDC, Centro para Controle e Prevenção de Doenças).</p>" +
                    $"<h2>Tem dúvidas?</h2>" +
                    $"<p>Acesse a nossa página de <a href='{vaccineFAQUrl}'>Perguntas frequentes (FAQ)</a> para saber mais sobre o seu Comprovante digital de vacinação contra a COVID-19.</p>" +
                    $"<h2>Mantenha-se informado.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Veja as informações mais recentes</a> sobre a COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>E-mail do representante oficial do Washington State Department of Health (Departamento de Saúde do estado de Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
               "hi" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>डिजिटल COVID-19 सत्यापन रिकॉर्ड </h1>" +
                    $"<p>डिजिटल COVID-19 सत्यापन रिकॉर्ड प्रणाली पर जाने के लिए धन्यवाद। आपके COVID-19 वैक्सीन रिकॉर्ड कोड को पुनः प्राप्त करने की लिंक {linkExpireHours} घंटे के लिए वैध है। आपके डिवाइस पर एक्सेस करने और सहेजने के बाद, QR कोड की समय-सीमा समाप्त नहीं होगी।</p>" +
                    $"<p><a href='{url}'>वैक्सीन रिकॉर्ड देखें </a></p>" +
                    $"<p>Centers for Disease Control and Prevention(रोग नियंत्रण और रोकथाम केंद्र) से <a href='{cdcUrl}'>स्वयं और दूसरों की रक्षा</a> करने के तरीके के बारे में और जानें।</p>" +
                    $"<h2>आपके कोई प्रश्न हैं?</h2>" +
                    $"<p>अपने डिजिटल COVID-19 वैक्सीन रिकॉर्ड के बारे में अधिक जानने के लिए हमारे <a href='{vaccineFAQUrl}'>अक्सर पूछे जाने वाले प्रश्नों (FAQ)</a> के पेज पर जाएँ।</p>" +
                    $"<h2>सूचित रहें।</h2>" +
                    $"<p>COVID-19 के बारे में <a href='{covidWebUrl}'>नवीनतम जानकारी देखें</a>। </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health (वाशिंगटन राज्य के स्वास्थ्य विभाग) का आधिकारिक ईमेल</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "de" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>COVID-19-Digitalzertifikat</h1>" +
                    $"<p>Danke für Ihren Besuch beim COVID-19-Digitalzertifikat-System. Der Link zum Abrufen Ihres COVID-19-Passes ist {linkExpireHours} Stunden lang gültig. Nachdem Sie den QR-Code aufgerufen und auf Ihrem Gerät gespeichert haben, läuft der Code nicht ab.</p>" +
                    $"<p><a href='{url}'>Impfpass anzeigen </a></p>" +
                    $"<p>Erfahren Sie von den Centers for Disease Control and Prevention (Zentren für Seuchenbekämpfung und Prävention) mehr darüber, wie Sie <a href='{cdcUrl}'>sich selbst und andere schützen</a> können.</p>" +
                    $"<h2>Haben Sie Fragen?</h2>" +
                    $"<p>Besuchen Sie unsere Seite mit <a href='{vaccineFAQUrl}'>häufig gestellten Fragen (FAQ)</a> , um mehr über Ihren digitalen COVID-19-Impfpass zu erfahren.</p>" +
                    $"<h2>Bleiben Sie auf dem Laufenden.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Sehen Sie sich die neuesten Informationen</a> über COVID-19 an.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Offizielle E-Mail-Adresse des Washington State Department of Health (Gesundheitsministerium des Bundesstaates Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ti" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>ዲጂታላዊ ናይ ኮቪድ-19 ክታበት መረጋገጺ መዝገብ</h1>" +
                    $"<p>ን ዲጂታላዊ ናይ ኮቪድ-19 መረጋገጺ መዝገብ ብምብጻሕኩም ነመስግነኩም። ናይ ኮቪድ-19 ክታበት መረጋገጺ መርከቢ ሊንክ ድማ ን {linkExpireHours} ሰዓታት ቅቡል እዩ። ሓንሳብ ምስ ኣተኹምን ኣብ መሳርሒትኩም ምስ ተዓቀበን ድማ፡ እቲ ናይ QR code ዕለቱ ኣይወድቕን እዩ።</p>" +
                    $"<p><a href='{url}'>ናይ ክታበት መዝገብ ርኣይዎ</a></p>" +
                    $"<p>ካብ Centers for disease Control and Prevention (CDC, ማእከላት ንምቊጽጻርን ምግታእን ጥዕና) ብዛዕባ <a href='{cdcUrl}'>ንካልኦትን ንገዛእ ርእስኹምን ምሕላው</a> ዝያዳ ተምሃሩ።</p>" +
                    $"<h2>ሕቶታት ኣለኩም ድዩ?</h2>" +
                    $"<p>ብዛዕባ ዲጂታላዊ ናይ ኮቪድ-19 ክታበት መረጋገጺ መዝገብ ዝያዳ ንምፍላጥ፡ ነቶም ናህና ገጽ ናይ ቀጻሊ ዝሕተቱ ሕቶታት <a href='{vaccineFAQUrl}'>ቀጻሊ ዝሕተቱ ሕቶታት</a> ብጽሕዎም።</h2>" +
                    $"<h2>ሓበሬታ ሓዙ።</h2>" +
                    $"<p>ብዛዕባ ኮቪድ-19 <a href='{covidWebUrl}'>ናይ ቀረባ ግዜ ሓበሬታ ራኣዩ</a> ኢኹም።</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>ወግዓዊ ናይ Washington State Department of Health (ክፍሊ ጥዕና ግዝኣት ዋሽንግተን) ኢ-መይል</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
               "te" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>డిజిటల్ కొవిడ్-19 ధృవీకరణ రికార్డ్</h1>" +
                    $"<p>డిజిటల్ కొవిడ్-19 ధృవీకరణ రికార్డ్ సిస్టమ్​ని సందర్శించినందుకు మీకు ధన్యవాదాలు. మీ కొవిడ్-19 వ్యాక్సిన్ రికార్డ్​ని తిరిగి పొందే లింక్ {linkExpireHours} గంటలపాటు చెల్లుబాటు అవుతుంది. మీరు యాక్సెస్ చేసుకొని, మీ పరికరంలో సేవ్ చేసిన తరువాత, QR కోడ్ గడువు తీరదు.</p>" +
                    $"<p><a href='{url}'>వ్యాక్సిన్ రికార్డ్​ని వీక్షించండి </a></p>" +
                    $"<p><a href='{cdcUrl}'>మిమ్మల్ని మరియు ఇతరుల నుంచి</a> సంరక్షించుకోవడం ఎలా అనే దానిని Centers for Disease Control and Prevention (సెంటర్స్ ఫర్ డిసీజ్ కంట్రోల్ అండ్ ప్రివెన్షన్) నుంచి తెలుసుకోండి. </p>" +
                    $"<h2>మీకు ఏమైనా ప్రశ్నలున్నాయా?</h2>" +
                    $"<p>డిజిటల్ కొవిడ్-19 వ్యాక్సిన్ రికార్డ్ గురించి మరింత తెలుసుకోవడానికి మా <a href='{vaccineFAQUrl}'>తరచుగా అడిగే ప్రశ్నలు (FAQ)</a> పేజీని సందర్శించండి.</p>" +
                    $"<h2>అవగాహనతో ఉండండి.</h2>" +
                    $"<p>కొవిడ్-19పై <a href='{covidWebUrl}'>తాజా సమాచారాన్ని వీక్షించండి</a>.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>అధికారిక Washington State Department of Health (వాషింగ్టన్ స్టేట్ డిపార్ట్​మెంట్ ఆఫ్ హెల్త్) ఇమెయిల్</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "sw" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Rekodi ya Kidijitali ya Uthibitishaji wa COVID-19</h1>" +
                    $"<p>Asante kwa kutembelea mfumo wa Rekodi ya Kidijitali ya Uthibitishaji wa COVID-19. Kiungo cha kupata msimbo wako wa chanjo ya COVID-19 kitakuwa amilifu kwa saa {linkExpireHours}. Mara tu imefikiwa na kuhifadhiwa kwenye kifaa chako, msimbo wa QR hautaisha muda.</p>" +
                    $"<p><a href='{url}'>Tazama Rekodi ya Chanjo .</a></p>" +
                    $"<p>Jifunze zaidi kuhusu jinsi ya <a href='{cdcUrl}'>kujilinda pamoja na wengine</a> kutoka Centers for Disease Control and Prevention (Vituo vya Udhibiti na Uzuiaji wa Ugonjwa).</p>" +
                    $"<h2>Una maswali?</h2>" +
                    $"<p>Tembelea ukurasa wa <a href='{vaccineFAQUrl}'>Maswali yetu Yanayoulizwa Mara kwa Mara (FAQ)</a>  ili kujifunza zaidi kuhusu Rekodi yako ya Kidijitali ya Chanjo ya COVID-19.</p>" +
                    $"<h2>Endelea Kupata Habari.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Tazama maelezo ya hivi karibuni</a> kuhusu COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Barua pepe Rasmi ya Washington State Department of Health (Idara ya Afya katika Jimbo la Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "so" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Diiwaanka Xaqiijinta Tallaalka COVID-19 ee Dhijitaalka Ah</h1>" +
                    $"<p>Waad ku mahadsan tahay soo booqashadaada Diiwaanka Xaqiijinta Tallaalka COVID-19 ee Dhijitaalka ah. Lifaaqa aad kula soo baxeyso koodhka diiwaanka tallaalka COVID-19 ayaa ansax ah oo shaqeynayo {linkExpireHours} saacadood. Marki la galo oo lagu keydiyo taleefankaaga, koodhka jawaabta degdegga ma dhacayo.</p>" +
                    $"<p><a href='{url}'>Arag Diiwaanka Tallaalka </a></p>" +
                    $"<p>Wax badan ka ogow sida <a href='{cdcUrl}'>ilaali adiga iyo dadka kale</a> oo ka socota Xarumaha a iyo Kahortagga Cudurrada (Centers for Disease Control and Prevention).</p>" +
                    $"<h2>Su'aalo ma qabtaa?</h2>" +
                    $"<p>Booqo boggeena (<a href='{vaccineFAQUrl}'>Su'aalaha Badanaa La Iswaydiiyo</a>)  si aad wax badan uga ogaato Diiwaankaaga Xaqiijinta Tallaalka COVID-19 ee Dhijitaalka ah.</p>" +
                    $"<h2>La Soco Xogta.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Arag Macluumaadki ugu danbeeyey</a>  oo ku saabsan COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Iimeelka Rasmiga Ee Washington State Department of Health (Waaxda Caafimaadka Gobolka Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "sm" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Faamaumauga Faamaonia o le KOVITI-19</h1>" +
                    $"<p>Faafetai mo le asiasi mai i faamaumauga faamaonia o le KOVITI-19. O le fesootaiga e maua ai faamaumauga o le KOVITI-19 e {linkExpireHours} itula lona aoga. A maua uma faamatalaga, ia faamauina, lelei ma sefe i lau masini, o le QR code e mafai ona faaogaina e le toe muta.</p>" +
                    $"<p><a href='{url}'>Silasila i faamaumauga o Tui puipui </a></p>" +
                    $"<p>Ia silafia lelei auala e <a href='{cdcUrl}'>puipuia ai oe ma isi</a> e ala mai i le Centers for Disease Control and Prevention (poo le Ofisa Tutotonu mo le Vaaiga o le Pepesi o Faamaʻi ma le Puipuia mai Faamaʻi).</p>" +
                    $"<h2>E iai ni fesili?</h2>" +
                    $"<p>Asiasi ane i mataupu e masani ona fesiligia (<a href='{vaccineFAQUrl}'>Fesili Masani ma Tali</a>) itulau e faalauteleina ai lou silafia i Fa’amaumauga Fa’amaonia o le KOVITI-19 i luga i Upega Tafa’ilagi</p>" +
                    $"<h2>Ia silafia pea.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Silasila i faamatalaga lata mai</a> o le KOVITI-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Imeli aloaia a le Washington State Department of Health (Matagaluega o le Soifua Maloloina a le Setete o Uosigitone)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "pa" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>ਡਿਜੀਟਲ ਕੋਵਿਡ-19 ਵੇਰਿਫਿਕੇਸ਼ਨ ਰਿਕਾਰਡਿਕਾਰਡ</h1>" +
                    $"<p>ਡਿਜੀਟਲ ਕੋਵਿਡ-19 ਵੇਰਿਫਿਕੇਸ਼ਨ ਰਿਕਾਰਡਿਕਾਰਡ ਸਿਸਟਮ 'ਤੇ ਆਉਣ ਲਈ ਤੁਹਾਡਾ ਧੰਨਵਾਦ। ਤੁਹਾਡੇ ਕੋਵਿਡ-19 ਵੈਕਸੀਨ ਰਿਕਾਰਡ ਕੋਡ ਨੂੰ ਮੁੜ ਪ੍ਰਾਪਤ ਕਰਨ ਲਈ ਲਿੰਕ {linkExpireHours} ਘੰਟਿਆਂ ਲਈ ਵੈਧ ਹੈ। ਇੱਕ ਵਾਰ ਤੁਹਾਡੇ ਡਿਵਾਈਸ 'ਤੇ ਐਕਸੈਸ ਕਰਨ ਅਤੇ ਸੁਰੱਖਿਅਤ ਹੋਣ ਤੋਂ ਬਾਅਦ, QR ਕੋਡ ਦੀ ਮਿਆਦ ਖ਼ਤਮ ਨਹੀਂ ਹੋਵੇਗੀ।</p>" +
                    $"<p><a href='{url}'>ਵੈਕਸੀਨ ਰਿਕਾਰਡ ਵੇਖੋ </a></p>" +
                    $"<p>Centers for Disease Control and Prevention (ਬਿਮਾਰੀ ਨਿਯੰਤ੍ਰਣ ਅਤੇ ਰੋਕਥਾਮ ਕੇਂਦਰ) ਤੋਂ <a href='{cdcUrl}'>ਆਪਣੀ ਅਤੇ ਦੂਜਿਆਂ ਦੀ ਰੱਖਿਆ ਕਰਨ</a>  ਦੇ ਤਰੀਕੇ ਬਾਰੇ ਹੋਰ ਜਾਣਕਾਰੀ ਪ੍ਰਾਪਤ ਕਰੋ।</p>" +
                    $"<h2>ਕੀ ਤੁਹਾਡੇ ਕੋਈ ਸਵਾਲ ਹਨ?</h2>" +
                    $"<p>ਆਪਣੇ ਡਿਜੀਟਲ ਕੋਵਿਡ-19 ਵੈਕਸੀਨ ਰਿਕਾਰਡ ਬਾਰੇ ਹੋਰ ਜਾਣਨ ਲਈ ਸਾਡੇ <a href='{vaccineFAQUrl}'>ਅਕਸਰ ਪੁੱਛੇ ਜਾਣ ਵਾਲੇ ਸਵਾਲ (FAQ)</a>  ਪੰਨੇ 'ਤੇ ਜਾਓ।</p>" +
                    $"<h2>ਸੂਚਿਤ ਰਹੋ।</h2>" +
                    $"<p>ਕੋਵਿਡ-19 ਬਾਰੇੇ <a href='{covidWebUrl}'>ਨਵੀਨਤਮ ਜਾਣਕਾਰੀ ਵੇਖੋ</a></p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health (ਵਾਸ਼ਿੰਗਟਨ ਸਟੇਟ ਸਿਹਤ ਵਿਭਾਗ) ਦਾ ਅਧਿਕਾਰਤ ਈਮੇਲ ਪਤਾ</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ps" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 dir='rtl' style='color: #C84C0E'>د ډیجیټل COVID-19 تائید ثبت</h1>" +
                    $"<p dir='rtl'>د ډیجیټل COVID-19 تائید ثبت سیسټم لیدو لپاره ستاسو مننه. ستاسو د COVID-19 واکسین ثبت کوډ بیرته ترلاسه کولو لینک د {linkExpireHours} ساعتونو لپاره د اعتبار وړ دی. یوځل چې ستاسو وسیلې ته لاسرسی ولري او خوندي شي، نو د QR کوډ به پای ته ونه رسیږي.</p>" +
                    $"<p dir='rtl'><a href='{url}'>د واکسین ریکارډ وګورئ </a></p>" +
                    $"<p dir='rtl'><a href='{cdcUrl}'>خپل ځان او نور خلک</a> څنګه وساتئ په اړه نور معلومات د ناروغیو کنټرول او مخنیوي مرکزونو (Centers for Disease Control and Prevention) څخه زده کړئ.</p>" +
                    $"<h2 dir='rtl'>ایا پوښتنې لرئ؟</h2>" +
                    $"<p dir='rtl'>د خپل ډیجیټل واکسین ثبت په اړه د نورې زده کړې لپاره زموږ په COVID-19 <a href='{vaccineFAQUrl}'>مکرر ډول پوښتل شوي پوښتنو (FAQ)</a>  پاڼې څخه لیدنه وکړئ.</p>" +
                    $"<h2 dir='rtl'>باخبر اوسئ.</h2>" +
                    $"<p dir='rtl'>دCOVID-19 په<a href='{covidWebUrl}'>اړه تازه معلومات وګورئ</a>.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p dir='rtl' style='text-align:center'>د واشنګټن ایالت د روغتیا ریاست (Washington State Department of Health) رسمي بریښنالیک</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ur" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 dir='rtl' style='color: #C84C0E'>ڈیجیٹل کووڈ-19 تصدیقی ریکارڈ</h1>" +
                    $"<p dir='rtl'>ڈیجیٹل کووڈ-19 تصدیقی ریکارڈ کا سسٹم ملاحظہ کرنے کا شکریہ۔ کووڈ-19 ویکسین ریکارڈ کا کوڈ وصول کرنے کا لنک {linkExpireHours} گھنٹے تک قابل استعمال گا۔ رسائی لے کر ڈیوائس میں محفوظ کرنے کے بعد کیو آر کوڈ کی میعاد ختم نہیں ہو گی۔</p>" +
                    $"<p dir='rtl'><a href='{url}'>ویکسین ریکارڈ دیکھیں</a></p>" +
                    $"<p dir='rtl'>Centers for Disease Control and Prevention (مراکز برائے امراض پر قابو اور انسداد) سے مزید سیکھیں کہ <a href='{cdcUrl}'>خود کو اور دوسروں کو کیسے محفوظ رکھا جائے</a></p>" +
                    $"<h2 dir='rtl'>سوالات ہیں؟</h2>" +
                    $"<p dir='rtl'>اپنے ڈیجیٹل کووڈ-19 ویکسین ریکارڈ کے متعلق مزید جاننے کے لئے ہمارا <a href='{vaccineFAQUrl}'>عمومی سوالات (FAQ)</a>  کا صفحہ ملاحظہ کریں۔</p>" +
                    $"<h2 dir='rtl'>آگاہ رہیں۔</h2>" +
                    $"<p dir='rtl'>کووڈ-19 کے متعلق <a href='{covidWebUrl}'>تازہ ترین معلومات دیکھیں<a/> </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p dir='rtl' style='text-align:center'>Washington State Department of Health (ریاست واشنگٹن محکمۂ صحت) کی سرکاری ای میل</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ne" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>डिजिटल कोभिड-19 प्रमाणीकरण रेकर्ड</h1>" +
                    $"<p>डिजिटल कोभिड-19 प्रमाणीकरण रेकर्ड प्रणाली हेर्नुभएको धन्यवाद। तपाईंको कोभिड-19 खोप रेकर्ड कोड पुनः प्राप्ति गर्ने लिङ्क {linkExpireHours} घण्टाकोसम्म मान्य हुन्छ। पहुँच गरेर तपाईंको यन्त्रमा बचत भइसकेपछि, QR कोडको म्याद समाप्त हुने छैन।</p>" +
                    $"<p><a href='{url}'>खोपसम्बन्धी रेकर्ड हेर्नुहोस्</a></p>" +
                    $"<p>Centers for Disease Control and Prevention (रोग नियन्त्रण तथा रोकथाम केन्द्रहरू) बाट <a href='{cdcUrl}'>आफ्नो र अन्य मानिसहरूको सुरक्षा गर्ने</a> तरिकाको बारेमा थप जान्नुहोस्।</p>" +
                    $"<h2>प्रश्नहरू छन्?</h2>" +
                    $"<p>आफ्नो डिजिटल कोभिड-19 खोप रेकर्डका बारेमा थप जान्नका लागि हाम्रो <a href='{vaccineFAQUrl}'>बारम्बार सोधिने प्रश्नहरू (FAQ)</a> को पृष्ठ हेर्नुहोस्।</p>" +
                    $"<h2>सूचित रहनुहोस्।</h2>" +
                    $"<p>कोभिड-19 बारे <a href='{covidWebUrl}'>नवीनतम जानकारी हेर्नुहोस्</a> ।</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>आधिकारिक Washington State Department of Health) वासिङ्गटन राज्यको स्वास्थ्य विभाग( को इमेल</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "mxb" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Tutu nuu kaa ndichí siki tu’un nasa iyo ní jín kue’e COVID-19</h1>" +
                    $"<p>Kuta’avi sa ja ni nde’e ní Tutu nuu kaa ndichí siki tu’un nasa iyo ní jín kue’e COVID-19. Enlace tágua nani’in ní tutu vacuna COVID-19 ní tiñu ji nuu {linkExpireHours} hora. Tú ja ni kivi ní de ni tava’a ní nuu kaa ndichí ní, código QR ma koo tiñu ji.</p>" +
                    $"<p><a href='{url}'>Kunde’e nasa iyo nda vacuna</a></p>" +
                    $"<p>Ni’in ka ní tu’un siki nasa <a href='{cdcUrl}'>koto yo maa yo ji sava ka ñayiví</a> nuu Centers for Disease Control and Prevention (CDC, Ve’e nuu jito ji jekani nda kue’e).</p>" +
                    $"<h2>A iyo tu’un jikatu’un ní</h2>" +
                    $"<p>Kunde’e ní nuu página <a href='{vaccineFAQUrl}'>nda tu’un jikatu’un ka (FAQ)</a> tágua ni’in ka ní tu’un siki nasa chi’in ni sivi ní nuu Tutu Tarjeta Vacuna COVID-19 ja iyo nuu Kaa ndichí.</p>" +
                    $"<h2>Ndukú ni tu’un.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Kunde’e ní tu’un jáá ka</a> siki COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Correo Washington State Department of Health (DOH, Ve’e nuu jito ja Sa’a tátan ñuu Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "mh" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Iaaṃ Jarom COVID-19 Jaak Jeje</h1>" +
                    $"<p>Koṃṃool erk bwe ilomej ko laam jarom COVID-19 jaak jeje kkar. Ko kkejel nan bok ami COVID-19 wa jeje ane ṃṃan bwe {linkExpireHours} awa. Juon iien ak kab lọmọọr nan ami tuuḷbọọk, ko QR kabro naaj jab ḷot.</p>" +
                    $"<p><a href='{url}'> Mmat Wa Jeje </a></p>" +
                    $"<p>Katak bar tarrin wāwee nan <a href='{cdcUrl}'>tokwanwa amimaanpa kab bar jet</a>  jan ko Centers for Disease Control and Prevention (Buḷōn bwe Nañinmej Kabwijer kab Deṃak).</p>" +
                    $"<h2>Jeban kajjitōk?</h2>" +
                    $"<p>Ilomei arro <a href='{vaccineFAQUrl}'>Jọkkutkut Kajjitōk Nawāwee (FAQ)</a>  ālāl nan katak bar jidik tarrin aim Iaam jarom COVID-19 Wa Jeje.</p>" +
                    $"<h2> Pād melele</h2>" +
                    $"<p> <a href='{covidWebUrl}'>Mmat ko rimwik kojjela</a>  ioo COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Wōpij Washington State Department of Health (Kutkutton konnaan jikuul in keenki) lota</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "mam" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Tz’ib’b’al te cheylakxta tej tx’u’j yab’il te COVID-19 toj xk’utz’ib’</h1>" +
                    $"<p>Chjontay ma tz’oktz tq’olb’e’na jqeya qkloj te Tz’ib’b’al te cheylakxta tej tx’u’j yab’il te COVID-19 toj jxk’utz’ib’. a enlace te tu’ ttiq’ay jte cheylakxta tej tx’u’j yab’ilo te COVID - 19 b’a’x teja toj jun q’ij b’ix jun qoniya mo toj {linkExpireHours} amb’il. Juxmaj uj ot tz’okxay ex ot ku’x tk’u’na toj jtey txk’utz’ib’, j - código QR ya mi kub’ najt.</p>" +
                    $"<p><a href='{url}'>Cheylakxta tej tz’ib’b’al tej tey tb’aq </a></p>" +
                    $"<p>Q’imatz kab’t q’umb’aj tumal tib’aj tzuj ti tten tu’ <a href='{cdcUrl}'>tok tklom tib’ay ex tu’ tok tklo’na qaj kab’t</a>  chu’ tzun qaj Ja’ te tej Meyolakta ex jtu’ Tajtz chi’j qaj Yab’il [Centers for Disease Control and Prevention, toy tyol me’x xjal].</p>" +
                    $"<h2>¿At qaj tajay tzaj tqanay?</h2>" +
                    $"<p>Tokx toj jqeya qkloj che qaj Qanb’al jakax (<a href='{vaccineFAQUrl}'>Qe Xjel Kukx in che Tzaj Qanin (FAQ)</a>) te tu’ ttiq’ay kab’t q’umb’aj tumal tib’aj jtey Ttz’ib’b’al te cheylakxta tej tx’u’j yab’il te COVID-19 toj xk’utz’ib’.</p>" +
                    $"<h2>Etkub’ tenay te ab’i’ chqil tumal tu’nay.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Ettiq’ay jq’umb’aj tumal ma’nxax nex q’umat</a>  tib’aj tx’u’j yab’il tej COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Correo electrónico te chqil Ja’ nik’ub’ aq’unt te Tb’anal xumlal tej Tnom te Washington [Washington State Department of Health, toj tyol me’x xjal] </p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "lo" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>ບັນທຶກການຢັ້ງຢືນ ພະຍາດ ໂຄວິດ-19 ແບບດີຈີຕອນ</h1>" +
                    $"<p>ຂອບໃຈທີ່ເຂົ້າເບິ່ງລະບົບບັນທຶກການຢັ້ງຢືນ ພະຍາດ ໂຄວິດ-19 ແບບດີຈີຕອນ. ລິ້ງເພື່ອຮຽກຄົ້ນລະຫັດບັນທຶກວັກຊີນປ້ອງກັນ ພະຍາດ ໂຄວິດ-19 ຂອງທ່ານແມ່ນໃຊ້ໄດ້ພາຍໃນເວລາ {linkExpireHours} ຊົ່ວໂມງ. ເມື່ອເຂົ້າເຖິງ ແລະ ບັນທຶກໄວ້ໃນອຸປະກອນຂອງທ່ານແລ້ວ, ລະຫັດ QR ຈະບໍ່ໝົດອາຍຸ.</p>" +
                    $"<p><a href='{url}'>ເບິ່ງບັນທຶກວັກຊີນ</a></p>" +
                    $"<p>ຮຽນ​ຮູ້​ເພີ່ມ​ເຕີມ​ກ່ຽວ​ກັບ​ວິ​ທີ <a href='{cdcUrl}'>ປ້ອງ​ກັນ​ຕົນ​ເອງ ​ແລະ ​ຄົນ​ອື່ນ</a> ​ຈາກ Centers for Disease Control and Prevention (ສູນ​ຄວບ​ຄຸມ​ ແລະ​ ປ້ອງ​ກັນ​ພະ​ຍາດ​).</p>" +
                    $"<h2>ມີ​ຄຳ​ຖາມ​ບໍ?</h2>" +
                    $"<p>ເຂົ້າເບິ່ງໜ້າ<a href='{vaccineFAQUrl}'>ຄໍາ​ຖາມ​ທີ່​ຖືກ​ຖາມ​ເລື້ອຍໆ (FAQ)</a> ເພື່ອຮຽນ​ຮູ້​ເພີ່ມເຕີມກ່ຽວກັບບັນທຶກວັກຊີນປ້ອງກັນ ພະຍາດ ໂຄວິດ-19 ແບບດີຈີຕອນຂອງທ່ານ.</p>" +
                    $"<h2>ຕິດຕາມຂ່າວສານ.</h2>" +
                    $"<p><a href='{covidWebUrl}'>ເບິ່ງຂໍ້ມູນຫຼ້າສຸດ</a> ກ່ຽວກັບ ພະຍາດ ໂຄວິດ-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>ອີເມວທາງການຂອງ Washington State Department of Health (ພະແນກ ສຸຂະພາບ)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "km" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>កំណត់ត្រា​ផ្ទៀងផ្ទាត់​ជំងឹ​ COVID-19 ជាទម្រង់​ឌីជីថល​</h1>" +
                    $"<p>អរគុណ​សម្រាប់​ការ​ចូល​​មកកាន់​ប្រព័ន្ធ​កំណត់ត្រា​ផ្ទៀងផ្ទាត់​ជំងឹ​ COVID-19 ជាទម្រង់​ឌីជីថល។​ តំណភ្ជាប់ដើម្បី​ទទួលបាន​មកវិញ​នូវ​កូដកំណត់ត្រា​វ៉ាក់សាំង​ជំងឺ​​ COVID-19 របស់អ្នក​ គឺ​មានសុពលភាព​រយៈពេល​ {linkExpireHours}ម៉ោង​។ កូដ QR នឹង​មិនផុត​សុពលភាព​ទេ​ នៅពេលបានចូលប្រើប្រាស់​និង​រក្សាទុក​ក្នុង​ឧបករណ៍​របស់អ្នក​។</p>" +
                    $"<p><a href='{url}'>ពិនិត្យ​មើល​កំណត់ត្រាវ៉ាក់សាំង</a></p>" +
                    $"<p>ស្វែងយល់បន្ថែម​អំពីរបៀប <a href='{cdcUrl}'>​ការពារខ្លួនអ្នក​និងអ្នកដទៃ​ព</a> Centers for Disease Control and Prevention (មជ្ឈមណ្ឌល​គ្រប់គ្រង​និង​បង្ការជំងឺ​)៖</p>" +
                    $"<h2>មានសំណួរមែនទេ?</h2>" +
                    $"<p>ចូលទៅកាន់ទំព័រ <a href='{vaccineFAQUrl}'>​​សំណួរចោទសួរជាញឹកញាប់ (FAQ)</a> របស់យើង​ដើម្បី​ស្វែងយល់​បន្ថែមអំពី​កំណត់ត្រា​វ៉ាក់សាំង​ COVID-19 ជាទម្រង់ឌីជីថល។</p>" +
                    $"<h2>បន្តទទួលបានដំណឹង​​។</h2>" +
                    $"<p><a href='{covidWebUrl}'>ពិនិត្យមើលព័ត៌មានថ្មីៗ​បំផុត</a> ស្តីពីជំងឺ​ COVID-19។</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>អ៊ីម៉ែលផ្លូវការរបស់​ Washington State Department of Health (ក្រសួងសុខាភិបាល​រដ្ឋ​វ៉ាស៊ីនតោន)។</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "kar" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>ဒံးကၠံၣ်တၢၣ်(လ) COVID-19 တၢ်ဆဲးကသံၣ်ဒီသဒၢတၢ်မၤနီၣ်မၤဃါ</h1>" +
                    $"<p>တၢ်ဘျုးလၢနလဲၤကွၢ် ဒံးကၠံၣ်တၢၣ်(လ) COVID-19 တၢ်အုၣ်သးတၢ်မၤနီၣ်မၤဃါ တၢ်မၤအ ကျဲသနူအဃိလီၤ. ပှာ်ဘျးစဲလၢနကဃုမၤန့ၢ်က့ၤန COVID-19 ကသံၣ်ဒီသဒၢတၢ်မၤနီၣ်မၤဃါ နီၣ်ဂံၢ် အံၤဖိးသဲစးလၢ {linkExpireHours} အဂီၢ်လီၤ. ဖဲနနုာ်လီၤမၤန့ၢ်ဒီးပာ်ကီၤဃာ်တၢ်လၢနပီးလီပူၤတစုန့ၣ်, QR နီၣ်ဂံၢ်န့ၣ်အဆၢကတီၢ် တလၢာ်ကွံာ်ဘၣ်.</p>" +
                    $"<p><a href='{url}'>ကွၢ်ကသံၣ်ဒီသဒၢတၢ်မၤနီၣ်မၤဃါ</a></p>" +
                    $"<p>မၤလိအါထီၣ်ဘၣ်ဃးဒီး နကဘၣ် <a href='{cdcUrl}'>ဒီသဒၢလီၤနနီၢ်ကစၢ်အသးဒီးပှၤအဂၤ</a> လၢ Centers for Disease Control and Prevention (တၢ်ဖီၣ်ဂၢၢ်ပၢဆှၢတၢ်ဆူးတၢ်ဆါဒီးတၢ်ဟ့ၣ်တၢ်ဒီသဒၢစဲထၢၣ်) ဒ်လဲၣ်န့ၣ်တက့ၢ်.</p>" +
                    $"<h2>တၢ်သံကွၢ်အိၣ်ဧါ.</h2>" +
                    $"<p>လဲၤကွၢ်ဖဲ တၢ်သံကွၢ်လၢတၢ်သံကွၢ်အီၤခဲအံၤခဲအံၤ (<a href='{vaccineFAQUrl}'>တၢ်သံကွၢ်လၢဘၣ်တၢ်သံကွၢ်အီၤခဲအံၤခဲအံၤတဖၣ် (FAQ)</a>) ကဘျံးပၤလၢ ကမၤလိအါထီၣ် ဘၣ်ဃးဒီးန ဒံးကၠံၣ်တၢၣ်(လ) COVID-19 ကသံၣ်ဒီသဒၢတၢ်မၤနီၣ်မၤဃါ န့ၣ်တက့ၢ်.</p>" +
                    $"<h2>သ့ၣ်ညါတၢ်ဘိးဘၣ်သ့ၣ်ညါထီဘိ</h2>" +
                    $"<p><a href='{covidWebUrl}'>ကွၢ်တၢ်ဂ့ၢ်တၢ်ကျိၤလၢခံကတၢၢ်</a> ဖဲ COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health(၀ၣ်ရှ့ၣ်တၢၣ်ကီၢ်စဲၣ်တၢ်အိၣ်ဆူၣ်အိၣ်ချ့ဝဲၤကျိၤ) အံမ့(လ)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "fj" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>iVolatukutuku Vakalivaliva ni iVakadinadina ni veika e Vauca na COVID-19</h1>" +
                    $"<p>Vinaka vakalevu na nomu sikova mai iVolatukutuku Vakalivaliva ni iVakadinadina ni veika e Vauca na COVID-19. Na isema mo rawa ni raica tale kina na na ivakadinadina ni veika e vauca na COVID-19 me baleti iko ena rawa ni vakayagataki ga ena loma ni {linkExpireHours} na aua. Ni sa laurai oti qai maroroi ina nomu kompiuta se talevoni, ena rawa ni vakayagataki tiko ga na QR code.</p>" +
                    $"<p><a href='{url}'><a href='{url}'>Raica na iVolatukutuku ni veika e vauca na iCula<a href='{url}'></a></p>" +
                    $"<p>Vulica e levu tale na tikina ena sala mo <a href='{cdcUrl}'>taqomaki iko kina vei ira eso tale</a>  mai na Centers for Disease Control and Prevention (Tabana ni Tatarovi kei na Veitaqomaki mai na Mate).</p>" +
                    $"<h2>Taro?</h2>" +
                    $"<p>Rai ena tabana e tiko kina na <a href='{vaccineFAQUrl}'>Taro e Tarogi Wasoma (FAQ)</a>  mo kila e levu tale na tikina e vauca na iVolatukutuku Vakalivaliva ni Veika e Vauca na COVID-19.</p>" +
                    $"<h2>Mo Kila na Veika e Yaco Tiko.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Kila na itukutuku vou duadua ena veika e vauca</a>  na COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>iMeli ni Washington State Department of Health (Tabana ni Bula ena Vanua o Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "fa" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 dir='rtl' style='color: #C84C0E'>نسخه دیجیتال گواهی واکسیناسیون COVID-19</h1>" +
                    $"<p dir='rtl'>بابت بازدید از سیستم «نسخه دیجیتال گواهی واکسیناسیون COVID-19»، از شما متشکریم. پیوند بازیابی کد گواهی واکسیناسیون COVID-19 شما به‌مدت {linkExpireHours} ساعت معتبر است. به‌محض اینکه کد QR را دریافت و آن را در دستگاهتان ذخیره کنید، این کد دیگر منقضی نمی‌شود.</p>" +
                    $"<p dir='rtl'><a href='{url}'> مشاهده گواهی واکسیناسیون</a></p>" +
                    $"<p dir='rtl'>برای کسب اطلاعات بیشتر درباره نحوه <a href='{cdcUrl}'>محافظت از خود و دیگران</a> (فقط انگیسی)، به وب‌سایت Centers for Disease Control and Prevention (مراکز کنترل بیماری و پیشگیری از آن) مراجعه کنید.</p>" +
                    $"<h2 dir='rtl'>پرسشی دارید؟</h2>" +
                    $"<p dir='rtl'>برای کسب اطلاعات بیشتر در‌مورد «نسخه دیجیتال گواهی واکسیناسیون COVID-19»، از صفحه <a href='{vaccineFAQUrl}'>سؤالات متداول (FAQ)</a> ما دیدن کنید.</p>" +
                    $"<h2 dir='rtl'>آگاه و مطلع بمانید.</h2>" +
                    $"<p dir='rtl'><a href='{covidWebUrl}'> جدیدترین اطلاعات</a>  مربوط به COVID-19 را مشاهده کنید.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p dir='rtl' style='text-align:center'>ایمیل رسمی Washington State Department of Health (اداره سلامت ایالت واشنگتن)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "prs" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 dir='rtl' style='color: #C84C0E'>سابقه دیجیتل تصدیق کووید-19</h1>" +
                    $"<p dir='rtl'>تشکر برای بازدید از سیستم سابقه دیجیتل تصدیق کووید-19. لینک دریافت مجدد کد سابقه واکسین کووید-19 برای {linkExpireHours} ساعت معتبر است. زمانی که دسترسی پیدا کرده و در دستگاه شما ذخیره گردید، کد پاسخ سریع منقضی نخواهد شد.</p>" +
                    $"<p dir='rtl'><a href='{url}'>مشاهده سابقه واکسین </a></p>" +
                    $"<p dir='rtl'>از Centers for Disease Control and Prevention (مراکز کنترول امراض و وقایه) درباره چگونه <a href='{cdcUrl}'>محافطت کردن از خود و دیگران</a>  بیشتر یاد بگیرید.</p>" +
                    $"<h2 dir='rtl'>سوالاتی دارید؟</h2>" +
                    $"<p dir='rtl'><a href='{vaccineFAQUrl}'>از صفحه سوالات مکرراً پرسیده شده ما (FAQ)</a>  برای یادگیری بیشتر درباره سابقه دیجیتل واکسین کووید-19 بازدید کنید.</p>" +
                    $"<h2 dir='rtl'>مطلع بمانید.</h2>" +
                    $"<p dir='rtl'><a href='{covidWebUrl}'>مشاهده آخرین معلومات</a>  درباره کووید-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p dir='rtl' style='text-align:center'>ایمیل رسمی Washington State Department of Health (اداره صحت عامه ایالت واشنگتن)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "chk" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Digital COVID-19 Afatan Record</h1>" +
                    $"<p>Kinisou ren om tota won ewe Digital COVID-19 Afatan Record System. Ewe link ika anen kopwe tongeni angei noum code ren om kopwe kuna noum taropwen apposen COVID-19 echok eoch kopwe eaea non {linkExpireHours} awa. Ika pwe ka fen tonong ika a nom porausen noum we fon ika kamputer, ewe QR code esap muchuno manamanin.</p>" +
                    $"<p><a href='{url}'>Katon Taropwen Appos</a></p>" +
                    $"<p>Awateino om sinei usun ifa om kopwe <a href='{cdcUrl}'>tumunu me epeti inisum me inisin ekkoch</a> seni ewe Centers for Disease Control and Prevention (Ofesin Nenien Nemenemen Semwen me Pinepinen).</p>" +
                    $"<h2>Mi wor om kapaseis?</h2>" +
                    $"<p>Feino katon ach kewe Kapas Eis Ekon Nap Ach Eis <a href='{vaccineFAQUrl}'>(Chechemeni kapas ais (FAQ))</a> pon ach we peich ren om kopwe awatenai om sinei usun noum Digital COVID-19 Afatan Record.</p>" +
                    $"<h2>Nonom nge Sisinei.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Katon minefon poraus</a> on COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>An Washington State Department of Health (Washington State Ofesin Pekin Safei) Email</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "my" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>ဒီဂျစ်တယ်လ် ကိုဗစ်-19 အတည်ပြုချက် မှတ်တမ်း</h1>" +
                    $"<p>ဒီဂျစ်တယ်လ် ကိုဗစ်-19 အတည်ပြုချက် မှတ်တမ်း စနစ် ကို ဝင်လေ့လာသည့်အတွက် ကျေးဇူးတင်ပါသည်။ သင့် ကိုဗစ်-19 ကာကွယ်ဆေး အတည်ပြုချက် ကုဒ် ကို ထုတ်ယူရန် လင့်ခ်မှာ {linkExpireHours} နာရီကြာ သက်တမ်းရှိပါသည်။ ဝင်သုံးပြီး သင့်ကိရိယာထဲတွင် သိမ်းဆည်းထားလျှင် ကျူအာ ကုဒ်သည် သက်တမ်းကုန်ဆုံးသွားလိမ့်မည် မဟုတ်ပါ။</p>" +
                    $"<p><a href='{url}'>ကာကွယ်ဆေး မှတ်တမ်း ကို ကြည့်မည</a></p>" +
                    $"<p>Centers for Disease Control and Prevention(ရောဂါထိန်းချုပ်ရေး နှင့် ကာကွယ်ရေး စင်တာများ)မှ <a href='{cdcUrl}'>သင့်ကိုယ်သင် နှင့် အခြားသူများကို ကာကွယ်နည်း</a> များအကြောင်းကို လေ့လာမည်။</p>" +
                    $"<h2>မေးခွန်းများရှိပါသလား။</h2>" +
                    $"<p>သင့် ဒီဂျစ်တယ်လ် ကိုဗစ်-19 ကာကွယ်ဆေး မှတ်တမ်းအကြောင်း ပိုမိုသိရှိလိုလျှင် ကျွန်ုပ်တို့၏ မေးလေ့မေးထရှိသောမေးခွန်းများ (<a href='{vaccineFAQUrl}'>မေးလေ့မေးထရှိသောမေးခွန်းများ</a>)  ကို ဝင်ကြည့်ပါ။</p>" +
                    $"<h2>အချက်အလက်သိအောင်လုပ်ထားပါ</h2>" +
                    $"<p><a href='{covidWebUrl}'>နောက်ဆုံးအချက်အလက်များကို ကြည့်မည်</a> ကိုဗစ်-19 အကြောင်း</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>တရားဝင် Washington State Department of Health(ဝါရှင်တန် ပြည်နယ် ကျန်းမာရေး ဌာန) အီးမေးလ်</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "am" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>ዲጂታል የ COVID-19 ማረጋገጫ መዝገብ</h1>" +
                    $"<p>የዲጂታል COVID-19 ማረጋገጫ መዝገብ ስርዓትን ስለጎበኙ እናመሰግናለን። የእርስዎን የ COVID-19 ክትባት መዝገብ ኮድ ለማውጣት ያለው ሊንክ ለ {linkExpireHours} ሰዓታት ያገለግላል። አንዴ አግኝተውት ወደ መሳሪያዎ ካስቀመጡት፣ የ QR ኮድዎ ጊዜው አያበቃም።</p>" +
                    $"<p><a href='{url}'>የክትባት መዝገብዎን ይመልከቱ </a></p>" +
                    $"<p>እንዴት <a href='{cdcUrl}'>እራስዎን እና ሌሎችን መጠበቅ</a>  እንደሚችሉ ከ Centers for Disease Control and Prevention (በሽታ ቁጥጥር እና መከላከያ ማእከል) የበለጠ ይወቁ።</p>" +
                    $"<h2>ጥያቄዎች አሉዎት?</h2>" +
                    $"<p>ስለ ዲጂታል የ COVID-19 ክትባት መዝገብ የበለጠ ለማወቅ የእኛን <a href='{vaccineFAQUrl}'>ተዘውትረው የሚጠየቁ ጥያቄዎች</a> ገጽ ይጎብኙ።</p>" +
                    $"<h2>መረጃ ይኑርዎት።</h2>" +
                    $"<p>በ COVID-19 ላይ <a href='{covidWebUrl}'>የቅርብ ጊዜውን መረጃ ይመልከቱ</a> ።</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>ይፋዊ የ Washington State Department of Health (የዋሺንግተን ግዛት የጤና መምሪያ) ኢሜይል</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "om" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Mirkaneessa Ragaa Dijitaalaa COVID-19</h1>" +
                    $"<p>Mala Mirkaneessa Ragaa Dijitaalaa COVID-19 ilaaluu keessaniif galatoomaa. Liinkin ragaa talaallii COVID-19 keessan deebisanii argachuuf yookin seevii gochuuf gargaaru sa’aatii {linkExpireHours}’f hojjata. Meeshaa itti fayyadamtan (device) irratti argachuun danda’amee erga seevii ta’een booda, koodin QR yeroon isaa irra hin darbu.</p>" +
                    $"<p><a href='{url}'>Ragaa Taalaallii Ilaalaa</a></p>" +
                    $"<p>Waa’ee akkamitti akka  <a href='{cdcUrl}'>ofii fi namoota biroo eegdan</a> caalmatti Giddu Gala To’annoo fi Ittisa Dhibee (Centers for Disease Control and Prevention) irraa baradhaa.</p>" +
                    $"<h2>Gaaffii qabduu?</h2>" +
                    $"<p>Waa’ee Mirkaneessa Ragaa Dijitaalaa COVID-19 gaaffii yoo qabaattan, fuula <a href='{vaccineFAQUrl}'>Gaaffilee Yeroo Heddu Gaafataman (FAQ)</a> ilaalaa.</p>" +
                    $"<h2>Odeeffannoo Argadhaa.</h2>" +
                    $"<p>COVID-19 ilaalchisee  <a href='{covidWebUrl}'>odeeffannoo haaraa ilaalaa</a>.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Imeelii seera qabeessa kan Muummee Fayyaa Naannoo Washingtan (Washington State Department of Health)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "to" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Digital COVID-19 Verification Record (Lēkooti Fakamo’oni Huhu Malu'i COVID-19)</h1>" +
                    $"<p>Mālō ho’o ‘a’ahi mai ki he fa’unga ko eni ‘oku tauhi ai ‘a e Lēkooti Fakamo’oni Huhu Malu’i COVID-19. Ko e link ke ma’u’aki ho’o lēkooti huhu malu’i COVID-19 ‘e ‘aonga ‘i he houa ‘e {linkExpireHours}. Ko ho’o ma’u pē mo tauhi ki ho’o me’angāué, he’ikai toe ta’e’aonga ‘a e QR code.</p>" +
                    $"<p><a href='{url}'>Vakai ki he Lēkooti Huhu Malu’i </a></p>" +
                    $"<p>Ako lahiange ki he founga ke <a href='{cdcUrl}'>malu’i koe mo e ni’ihi kehe</a>  meí he Centers for Disease Control and Prevention (Senitā ki hono Mapule’i ‘a e Mafola ‘a e Mahakí).</p>" +
                    $"<h2>‘I ai ha ngaahi fehu’i?</h2>" +
                    $"<p>Vakai ki he’emau peesi Ngaahi Fehu’i ‘oku Fa’a ‘Eke Mai (<a href='{vaccineFAQUrl}'>Ngaahi Fehu’i ‘oku fa’a ‘Eke Mai</a>) ke toe ‘ilo lahiange fekau’aki mo ho’o Lēkooti Huhu Malu’i COVID-19.</p>" +
                    $"<h2>‘Ilo’i Maʻu Pē.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Vakai ki he fakamatala fakamuimui tahá</a> ’i he COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>'Imeili Faka'ofisiale Washington State Department Of Health (Potungāue Mo’ui ‘a e Siteiti ‘o Uasingatoní)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ta" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>மின்னணு Covid-19 சரிபார்ப்புப் பதிவு</h1>" +
                    $"<p>மின்னணு Covid-19 சரிபார்ப்புப் பதிவு முறையைப் பார்வையிட்டதற்கு நன்றி. உங்கள் Covid-19 தடுப்பூசி பதிவுக் குறியீட்டை மீட்டெடுப்பதற்கான இணைப்பு {linkExpireHours} மணிநேரத்திற்கு செல்லுபடியாகும். இணைப்பை அணுகி உங்கள் சாதனத்தில் சேமித்துவிட்டால், QR குறியீடு காலாவதியாகாது.</p>" +
                    $"<p><a href='{url}'>தடுப்பூசி பதிவைப் பார்க்கவும்</a></p>" +
                    $"<p>நோய் கட்டுப்பாடு மற்றும் தடுப்பு மையங்களில் இருந்து <a href='{cdcUrl}'>உங்களையும் பிறரையும் பாதுகாப்பது </a> Centers for Disease Control and Prevention )எப்படி என்பது பற்றி மேலும் அறிக) </p>" +
                    $"<h2>கேள்விகள் உள்ளதா?</h2>" +
                    $"<p>உங்கள் மின்னணு கொவிட்-19 தடுப்பூசி பதிவைப் பற்றி மேலும் அறிய, எங்களின் <a href='{vaccineFAQUrl}'>அடிக்கடி கேட்கப்படும் கேள்விகள் (FAQ)</a> பக்கத்தைப் பார்வையிடவும்.</p>" +
                    $"<h2>தகவலை அறிந்து இருங்கள்.</h2>" +
                    $"<p><a href='{covidWebUrl}'>சமீபத்திய தகவலைப் பார்க்கவும்</a> கொவிட்-19 பற்றி.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>அதிகாரப்பூர்வ Washington State Department of Health (வாஷிங்டன் மாநில சுகாதாரத் துறை) மின்னஞ்சல்</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "hmn" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Kev Txheeb Xyuas Ntaub Ntawv Sau Tseg Txog Kab Mob COVID-19 Ua Dis Cis Tauj</h1>" +
                    $"<p>Ua tsaug rau kev mus saib kev ua hauj lwm rau Kev Txheeb Xyuas Ntaub Ntawv Sau Tseg Txog Kab Mob COVID-19 Ua Dis Cis Tauj. Txoj kab txuas nkag mus txhawm rau rub koj tus khauj ntaub ntawv sau tseg txog kev txheeb xyuas kab mob COVID-19 yog siv tau li {linkExpireHours} xuab moos. Thaum tau nkag mus thiab tau muab kaw cia rau koj lub xov tooj lawm, tus khauj QR yuav tsis paub tag sij hawm lawm.</p>" +
                    $"<p><a href='{url}'>Saib Ntaub Ntawv Sau Tseg Txog Tshuaj Tiv Thaiv Kab Mob </a></p>" +
                    $"<p>Kawm paub txiv txog txoj hauv kev los <a href='{cdcUrl}'>pov thaiv koj tus kheej thiab lwm tus neeg</a> los ntawm Centers for Disease Control and Prevention (Cov Chaw Tswj thiab Pov Thaiv Kab Mob).</p>" +
                    $"<h2>Puas muaj cov lus nug?</h2>" +
                    $"<p>Mus saib peb nplooj vev xaib muaj <a href='{vaccineFAQUrl}'>Cov Lus Nug Uas Nquag Nug (FAQ)</a> txhawm rau kawm paub ntxiv txog koj li Ntaub Ntawv Sau Tseg Txog Tshuaj Tiv Thaiv Kab Mob COVID-19 Ua Dis Cis Tauj</p>" +
                    $"<h2>Soj Qab Saib Kev Paub.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Saib cov ntaub ntawv tawm tshiab tshaj plaws</a> txog kab mob COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Tus Email Siv Raws Cai Ntawm Xeev Washington State Department of Health (Chav Hauj Lwm ntsig txog Kev Noj Qab Haus Huv)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "th" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                     $"<h1 style='color: #C84C0E'>บันทึกการยืนยันเกี่ยวกับโควิด-19 แบบดิจิทัล</h1>" +
                     $"<p>ขอขอบคุณที่เยี่ยมชมระบบบันทึกการยืนยันเกี่ยวกับโควิด-19 แบบดิจิทัล ลิงก์การเรียกดูรหัสบันทึกการฉีดวัคซีนป้องกันโควิด-19 ของคุณมีอายุ {linkExpireHours} ชั่วโมง เมื่อคุณได้เข้าถึงและบันทึกลงในอุปกรณ์ของคุณแล้ว คิวอาร์โค้ดจะไม่หมดอายุ</p>" +
                     $"<p><a href='{url}'>ดูบันทึกวัคซีน</a></p>" +
                     $"<p>เรียนรู้เพิ่มเติมเกี่ยวกับวิธีการ<a href='{cdcUrl}'>ป้องกันตัวเองและผู้อื่น</a> จาก Centers for Disease Control and Prevention (ศูนย์ควบคุมและป้องกันโรค)</p>" +
                     $"<h2>มีคำถามหรือไม่</h2>" +
                     $"<p>โปรดไปยังส่วน<a href='{vaccineFAQUrl}'>คำถามที่พบบ่อย (FAQ)</a> เพื่อเรียนรู้เพิ่มเติมเกี่ยวกับบันทึกการฉีดวัคซีนป้องกันโควิด-19 แบบดิจิทัลของคุณ</p>" +
                     $"<h2>คอยติดตามข่าวสาร</h2>" +
                     $"<p><a href='{covidWebUrl}'>ดูข้อมูลล่าสุด</a> เกี่ยวกับโควิด-19</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                     $"<hr>" +
                     $"<footer><p style='text-align:center'>อีเมลอย่างเป็นทางการของ Washington State Department of Health (กรมอนามัยของรัฐวอชิงตัน)</p>" +
                     $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                _ => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                  $"<h1 style='color: #C84C0E'>Digital COVID-19 Verification Record</h1>" +
                  $"<p>Thank you for visiting the Digital COVID-19 Verification Record system. The link to retrieve your COVID-19 verification record code is valid for {linkExpireHours} hours. Once accessed and saved to your device, the QR code will not expire.</p>" +
                  $"<p><a href='{url}'>View Verification Record</a></p>" +
                  $"<p>Learn more about how to <a href='{cdcUrl}'>protect yourself and others</a> from the Centers for Disease Control and Prevention.</p>" +
                  $"<h2>Have questions?</h2>" +
                  $"<p>Visit our <a href='{vaccineFAQUrl}'>Frequently Asked Questions (FAQ)</a> page to learn more about your Digital COVID-19 Verification Record.</p>" +
                  $"<h2>Stay Informed.</h2>" +
                  $"<p><a href='{covidWebUrl}'>View the latest information</a> on COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                  $"<hr>" +
                  $"<footer><p style='text-align:center'>Official Washington State Department of Health e-mail</p>" +
                  $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>"
            };
        }

        public static string FormatNotFoundSms(string lang, string phoneNumber)
        {
            phoneNumber = "\u200e" + phoneNumber;
            return lang switch
            {
                "es" => $"Recientemente solicitó un Registro digital de verificación de COVID-19 del estado. Desafortunadamente, la información que ingresó no coincide con la que tenemos en nuestro sistema. Comuníquese al {phoneNumber} y, luego, presione numeral (#) para obtener ayuda a fin de que su información de contacto coincida con los registros.",
                "zh" => $"您最近向州政府请求过数字 COVID-19 验证记录。很遗憾，您提供的信息与我们系统中的信息不符。请拨打 {phoneNumber} 与我们联系，按 # 可获取将您的记录与您的联系信息进行匹配的援助。",
                "zh-TW" => $"您最近向州政府請求過數位 COVID-19 驗證記錄。很遺憾，您提供的資訊與我們系統中的資訊不符。請撥打 {phoneNumber} 與我們連絡，按 # 獲取援助以將您的記錄與您的連絡資訊進行匹配。 ",
                "ko" => $"귀하는 최근 주정부에 디지털 COVID-19 인증 기록을 요청하셨습니다. 유감스럽게도 귀하가 제공하신 정보는 저희 시스템상 정보와 일치하지 않습니다. {phoneNumber} 번으로 전화하여, # 버튼을 누르고 귀하의 기록과 연락처 정보 일치를 확인하는 데 도움을 받으시기 바랍니다.",
                "vi" => $"Gần đây bạn yêu cầu hồ sơ xác nhận COVID-19 kỹ thuật số từ tiểu bang. Rất tiếc, thông tin mà bạn cung cấp không khớp với thông tin có trong hệ thống của chúng tôi. Hãy liên hệ với chúng tôi theo số {phoneNumber}, nhấn # để được trợ giúp khớp thông tin hồ sơ với thông tin liên lạc của bạn.",
                "ar" => $"لقد قمت مؤخرًا بطلب الحصول على سجل التحقق الرقمي من فيروس كوفيد-19 من الولاية. ولكن للأسف، المعلومات التي قمت بتقديمها لا تتطابق مع المعلومات الموجودة على نظامنا. تواصل معنا على الرقم التالي {phoneNumber} واضغط على الرمز # للحصول على مساعدة في تحقيق التطابق بين سجلك ومعلومات التواصل الخاصة بك.",
                "tl" => $"Kamakailan kang humiling ng digital na talaan ng pagberipika para sa COVID-19 mula sa estado. Sa kasamaang-palad, hindi tugma ang impormasyong ibinigay mo sa impormasyong nasa system namin. Makipag-ugnayan sa amin sa {phoneNumber}, at pindutin ang # para sa tulong sa pagtugma ng talaan mo sa iyong impormasyon sa pakikipag-ugnayan.",
                "ru" => $"Недавно вы запросили у штата цифровую запись о вакцинации от COVID-19. К сожалению, предоставленная вами информация не совпадает с информацией в нашей системе. Свяжитесь с нами по телефону {phoneNumber} и нажмите #, чтобы получить помощь в сверке данных, указанных в вашей записи, с вашей контактной информацией.",
                "ja" => $"あなたはワシントン州のCOVID-19ワクチン接種電子記録を依頼されましたが、入力された情報はシステムに登録されている情報と一致しません。{phoneNumber} に電話して#を押すと、ご自身の記録と連絡先情報を一致させるためのサポートが受けられます。",
                "fr" => $"Vous avez récemment demandé une Attestation numérique de vaccination COVID-19 délivrée par l'État. Malheureusement, les informations que vous avez fournies ne correspondent pas à celles qui figurent dans notre système. Contactez-nous au {phoneNumber}, appuyez sur # pour obtenir de l'aide afin d'associer votre attestation à vos coordonnées. ",
                "tr" => $"Yakın zamanda eyaletten bir Dijital COVID-19 Doğrulama Kaydı istediniz. Maalesef verdiğiniz bilgiler sistemimizdeki bilgilerle eşleşmiyor. {phoneNumber} numarasından bize ulaşın ve kaydınızın iletişim bilgileriyle eşleşmesi konusunda yardım almak için # tuşuna basın.",
                "uk" => $"Нещодавно ви надіслали запит на отримання доступу до електронного запису про підтвердження вакцинації від COVID-19 державного зразка. На жаль, інформація, яку ви надали, не збігається з інформацією в нашій системі. Зв’яжіться з нами за номером {phoneNumber} і натисніть #, щоб допомогти звірити контактну інформацію у вашому записі.",
                "ro" => $"Ați solicitat recent un certificat digital COVID-19 de la stat. Din păcate, informațiile furnizate de dvs. nu corespund cu datele din sistemul nostru. Contactați-ne la {phoneNumber} și apăsați # pentru asistență privind potrivirea dintre fișa dvs. și datele de contact.",
                "pt" => $"Recentemente, você solicitou ao estado um comprovante digital de vacinação contra a COVID-19. Infelizmente, as informações fornecidas não correspondem às informações em nosso sistema. Entre em contato conosco pelo número {phoneNumber} e pressione # para obter ajuda com a correspondência entre os dados de contato e as informações em seu comprovante.",
                "hi" => $"आपने हाल ही में राज्य से डिजिटल COVID-19 वेरिफिकेशन रिकॉर्ड का अनुरोध किया है। दुर्भाग्य से, आपके द्वारा प्रदान की गई जानकारी हमारे सिस्टम में मौजूद जानकारी से मेल नहीं खाती है। अपने रिकॉर्ड को आपकी संपर्क जानकारी से मिलान करने में मदद के लिए {phoneNumber} पर हमसे संपर्क करें, # दबाएँ।",
                "de" => $"Sie haben kürzlich ein COVID-19-Digitalzertifikat vom Bundesstaat angefordert. Leider stimmen die von Ihnen gemachten Angaben nicht mit den Informationen in unserem System überein. Kontaktieren Sie uns unter {phoneNumber}, drücken Sie die #-Taste, um Hilfe beim Abgleich Ihres Protokolls mit Ihren Kontaktinformationen zu erhalten.",
                "ti" => $"ኣብ ቀረባ እዋን ዲጂታላዊ ናይ ኮቪድ-19 መረጋገጺ መዝገብ ካብ ስተይት ጠሊብኩም። ኣጋጣሚ ኮይኑግን፡ እቲ ንስኹም ዝሃብክምዎ ሓበሬታ ከኣ ምስ’ቲ ኣብ ሲስተምና ዘሎ ሓበሬታ ኣይተሰማመዐን። ንዓና ኣብ {phoneNumber} ተወከሱና፡ ናትኩም መዝገብ ምስ ናይ መወከሲ ሓበሬታ ንምዝማድ ሓገዝ ንምርካብ # ጠውቑ።",
                "te" => $"మీరు ఇటీవల రాష్ట్రం నుంచి డిజిటల్ కొవిడ్-19 ధృవీకరణ రికార్డ్​ని అభ్యర్ధించారు. దురదృష్టవశాత్తు, మీరు అందించిన సమాచారం మా సిస్టమ్​లోని సమాచారంతో జతకావడం లేదు. మీ రికార్డ్​ని మీ సంప్రదించు సమాచారంతో జతచేయడంలో సాయపడేందుకు, దయచేసి మాకు {phoneNumber} ద్వారా కాల్ చేసి, # ప్రెస్ చేయండి.",
                "sw" => $"Hivi karibuni uliomba rekodi ya kidijitali ya uthibitishaji wa COVID-19 kutoka kwa jimbo. Kwa bahati mbaya, maelezo uliyotoa hayalingani na maelezo yaliyopo kwenye mfumo wetu. Wasiliana nasi kupitia {phoneNumber}, kisha ubofye # ili kupata usaidizi wa kulinganisha rekodi yako na maelezo yako ya mawasiliano.",
                "so" => $"Waxaad dhawaan gobolka ka codsatay diiwaanka xaqiijinta tallaalka COVID-19 ee dhijitaalka ah. Nasiib daro, macluumaadka aad bixisay ma waafaqsana macluumaadka kujira nidaamkeena. Nagala soo xiriir {phoneNumber}, kadib riix # si aad u hesho caawimaada islahaysiinta diiwaankaaga iyo macluumaadkaaga xiriirka.",
                "sm" => $"Sa e talosagaina talu ai nei ni fa’amaumauga fa’amaonia o le KOVITI-19 i luga o Upega Tafa’ilagi mai le setete. E faamalie atu o faamatalaga na e tuuina mai e le tutusa ma faamaumauga i totonu o le matou polokalame. Faafesootai mai le numera {phoneNumber}, oomi # e maua ai se fesoasoani ina tutusa ou faamaumauga ma faamatalaga tusitusia.",
                "pa" => $"ਤੁਸੀਂ ਹਾਲ ਹੀ ਵਿੱਚ ਸਟੇਟ ਤੋਂ ਇੱਕ ਡਿਜ਼ੀਟਲ ਕੋਵਿਡ-19 ਵੈਰੀਫਿਕੇਸ਼ਨ ਰਿਕਾਰਡ ਲਈ ਬੇਨਤੀ ਕੀਤੀ ਸੀ। ਬਦਕਿਸਮਤੀ ਨਾਲ, ਤੁਹਾਡੇ ਦੁਆਰਾ ਪ੍ਰਦਾਨ ਕੀਤੀ ਗਈ ਜਾਣਕਾਰੀ ਸਾਡੇ ਸਿਸਟਮ ਵਿੱਚ ਮੌਜੂਦ ਜਾਣਕਾਰੀ ਨਾਲ ਮੇਲ ਨਹੀਂ ਖਾਂਦੀ। ਆਪਣੇ ਰਿਕਾਰਡ ਨੂੰ ਆਪਣੀ ਸੰਪਰਕ ਜਾਣਕਾਰੀ ਨਾਲ ਮਿਲਾਉਣ ਵਿੱਚ ਮਦਦ ਲਈ ਸਾਡੇ ਨਾਲ {phoneNumber} 'ਤੇ ਸੰਪਰਕ ਕਰੋ, ਅਤੇ # ਦਬਾਓ।",
                "ps" => $"تاسو پدې وروستیو کې له دولت څخه د ډیجیټل COVID-19 تائید ثبت غوښتنه کړې. له بده مرغه هغه معلومات چې تاسو چمتو کړي زموږ په سیسټم کې د معلوماتو سره سمون نلري. موږ سره په {phoneNumber} اړیکه ونیسئ، ستاسو د اړیکو معلوماتو سره ستاسو د ثبت په سمون کې د مرستې لپاره # کېکاږئ.",
                "ur" => $"آپ نے حال ہی میں ریاست سے ڈیجیٹل کووڈ-19 تصدیقی ریکارڈ کی درخواست کی ہے۔ بدقسمتی سے آپ کی فراہم کردہ معلومات ہمارے سسٹم میں موجود معلومات سے مماثلت نہیں رکھتیں۔ اپنے ریکارڈ کو رابطے کی معلومات سے ملانے کے لئے ہم سے {phoneNumber} پر رابطہ کریں اور # دبائیں۔",
                "ne" => $"तपाईंले हालै राज्यबाट डिजिटल कोभिड-19 प्रमाणीकरण रेकर्डको लागि अनुरोध गर्नुभयो। दुर्भाग्यवश, तपाईंले उपलब्ध गराउनुभएको जानकारी हाम्रो प्रणालीमा भएको जानकारीसँग मेल खाँदैन। तपाईंको रेकर्डलाई तपाईंको सम्पर्क जानकारीसँग मिलने बनाउनमा मद्दतका लागि {phoneNumber} मा हामीलाई सम्पर्क गर्नहोस्, # थिच्नुहोस्।",
                "mxb" => $"Iyo jaku kivi ja ni jikan ní in tutu nuu kaa ndichí siki tu’un nasa iyo ní jín kue’e COVID-19 ja iyo nuu ñuu. Kueka kuu ja, tu’un ja ni taji ní nduu kitan ji tu’un ja neva’a sa. Ka’an ní jín nda sa nuu yokaa nuu {phoneNumber}, ñukua de kuaxin ní # tágua kuu sa’a yo ja kita’an tu’un ja taji ní ji ja neva’a sa.",
                "mh" => $"Kwar kajitōke Rekoot in Kein Kamool COVID-19 eo am ilo online jen kien. Būromōj, melele ko kwar likit ejjab juon wōt ibben melele ko ilo system in ad. Kūrtok kōj ilo {phoneNumber}, jiped # n̄an am bōk jibān̄ ko n̄an am bukōt rekoot eo am.",
                "mam" => $"Ma’nxax nokx tqanay jun Tqanil toj Yolb’il tun Tjyet COVID-19. Aj nojsamay, jq’umb’aj tumal xi q’o’ mi nel joniy tuya jq’umb’aj tumal toj jqeya qkloj te xk’utz’ib’. Ttzaj q’ajt qeya qe toj tajlal lu {phoneNumber}, peq’ak’ax jlu # te tu’ ttiq’ay onb’al te tu’ntza tel joniy jtey ttz’ib’b’al tuya jtey tq’umb’aj tumal te tyolb’alay.",
                "lo" => $"ເມື່ອບໍ່ດົນມານີ້ທ່ານໄດ້ຮ້ອງຂໍ ບັນທຶກການຢັ້ງຢືນ ພະຍາດ ໂຄວິດ-19 ແບບດີຈີຕອນຈາກລັດ. ໂຊກບໍ່ດີ, ຂໍ້ມູນທີ່ທ່ານສະໜອງໃຫ້ບໍ່ກົງກັບຂໍ້ມູນຢູ່ໃນລະບົບຂອງພວກເຮົາ. ຕິດຕໍ່ຫາພວກເຮົາທີ່ {phoneNumber}, ກົດ # ເພື່ອຂໍຄວາມຊ່ວຍເຫຼືອໃນການເຮັດໃຫ້ ບັນທຶກຂອງທ່ານກັບຂໍ້ມູນຕິດຕໍ່ຂອງທ່ານຊອດຊ່ອງກັນ.",
                "km" => $"ថ្មីៗនេះ​ អ្នក​បានស្នើសុំកំណត់ត្រា​ផ្ទៀងផ្ទាត់​ជំងឹ​ COVID-19 ជាទម្រង់​​ឌីជីថលពីរដ្ឋ​។​ គួរឲ្យសោកស្តាយ ព័ត៌មានដែលអ្នក​បានផ្តល់ជូននោះ​ មិនត្រូវគ្នា​ជាមួយ​​នឹង​ព័ត៌មានក្នុង​ប្រព័ន្ធ​យើង​ទេ​។ ទាក់ទង​មក​​យើង​តាមរយៈលេខ​ {phoneNumber} ចុចញ្ញា​ # សម្រាប់ជំនួយក្នុងការ​ផ្ទៀងផ្ទាត់​កំណត់ត្រារបស់អ្នក​ជាមួយនឹង​ព័ត៌មានទំនាក់ទំនង​របស់អ្នក​។",
                "kar" => $"ဖဲတယံာ်ဘၣ်နဃ့ထီၣ် ဒံးကၠံၣ်တၢၣ်(လ) COVID-19 တၢ်အုၣ်သးတၢ်မၤနီၣ်မၤဃါ လၢကီၢ်စဲၣ်န့ၣ်လီၤ. လၢတၢ်တဘူၣ်ဂ့ၤတီၢ်ဘၣ်အပူၤ, တၢ်ဂ့ၢ်တၢ်ကျိၤလၢနဟ့ၣ်လီၤန့ၣ် တဘၣ်လိာ်ဒီး တၢ်ဂ့ၢ်တၢ်ကျိၤလၢပတၢ်မၤအကျဲသနူအပူၤဘၣ်. ဆဲးကျၢပှၤဖဲ {phoneNumber}, စံၢ်လီၤ # လၢတၢ်မၤစၢၤအဂီၢ် လၢကဘၣ်လိာ်ဒီးနတၢ် မၤနီၣ်မၤဃါဒီးနတၢ်ဆဲးကျၢတၢ်ဂ့ၢ်တၢ်ကျိၤတက့ၢ်.",
                "fj" => $"O se qai kerea ga mo raica na ivolatukutuku vakalivaliva ni ivakadinadina ni veika e vauca na COVID-19. Na itukutuku oni vakarautaka e sega ni tautauvata kei na kena e tiko vei keitou. Veitaratara mai vei keitou ena {phoneNumber}, tabaka # me rawa ni keitou veivuke me salavata na itukutuku o vakarautaka kei na itukutuku ni veitaratara me baleti iko e tiko vei keitou.",
                "fa" => $"شما به‌تازگی نسخه دیجیتال گواهی واکسیناسیون COVID-19 خود را از ایالت درخواست کرده‌اید. متأسفانه، اطلاعاتی که ارائه کرده‌اید با اطلاعات موجود در سیستم ما مطابقت ندارد. برای کسب راهنمایی در‌مورد مطابقت گواهی واکسیناسیون با اطلاعات تماستان، با ما به شماره {phoneNumber} تماس بگیرید و دکمه # را فشار دهید.",
                "prs" => $"به تازگی شما یک سابقه دیجیتل تصدیق کووید-19 را از دولت درخواست کردید. متاسفانه، اطلاعاتی که شما ارائه نمودید با اطلاعات داخل سیستم ما مطابقت ندارد. از طریق شماره {phoneNumber} با ما تماس بگیرید، # را برای کمک به منظور مطابق دادن سابقه خود با اطلاعات تماس خود فشار دهید.",
                "chk" => $"Ke keran chok tungor echo noum taropwen apposen COVID-19 online seni ewe state. Nge, ewe poraus ke awora ese mes ngeni met poraus mi nom non ach ei system ika nenien aisois. Kokori kich ren ei nampan fon {phoneNumber}, tiki # ren aninisin kuta met epwe mes ngeni ren porausom me ifa usun ach sipwe kokoruk.",
                "my" => $"မကြာသေးမီက သင်သည် ပြည်နယ်ထံမှ ဒီဂျစ်တယ်လ် ကိုဗစ်-19 အတည်ပြုချက် မှတ်တမ်း တစ်ခုကို တောင်းဆိုခဲ့ပါသည်။ ကံမကောင်းစွာဖြင့် သင်ပေးထားသော အချက်အလက်သည် ကျွန်ုပ်တို့ စနစ်အတွင်းရှိ အချက်အလက် နှင့် ကိုက်ညီမှုမရှိပါ။ သင့် ဆက်သွယ်ရန် အချက်အလက်အား သင့်မှတ်တမ်းနှင့်ကိုက်ညီစေရန် အကူအညီလိုလျှင် {phoneNumber} ထံဖုန်းဆက်ပြီး # ကို ဖိပါ။ ",
                "am" => $"በቅርቡ ከግዛቱ የዲጂታል COVID-19 ማረጋገጫ መዝገብ ጠይቀዋል። እንደ አለመታደል ሆኖ፣ ያቀረቡት መረጃ በእኛ ስርዓት ውስጥ ካለው መረጃ ጋር አይዛመድም። በ {phoneNumber} ያግኙን፣ መዝገብዎን ከመገኛ መረጃዎ ጋር ለማዛመድ እገዛ ካስፈለግዎ #ን ይጫኑ።",
                "om" => $"Dhiyeenyatti naannicha irraa mirkaneessa ragaa dijitaalaa COVID-19 gaafattaniirtu. Akka carraa ta’ee, odeeffannoon isin laattan kan siistama keenya keessa jiruun wal hin simu. Ragaa keessan qunnamtii odeeffannoo keessan wajjin akka wal simu gochuuf gargaarsa yoo barbaaddan, {phoneNumber} irratti nu qunnamaa, # tuqaa.",
                "to" => $"Na’a ke toki kole ni mai ha tatau ho’o lēkooti fakamo’oni huhu malu’i COVID-19. Me’apango, ko e fakamatala ‘oku ke ‘omí ‘oku ‘ikai tatau ia mo e fakamatala ‘oku mau tauhí. Fetu’utaki mai ki he {phoneNumber}, lomi ‘a e # ki ha tokoni ki hono fakahoa ho’o lēkootí ki ho’o fakamatala fetu’utakí.",
                "ta" => $"நீங்கள் சமீபத்தில் மாநிலத்திடம் மின்னணு கொவிட்-19 சரிபார்ப்புப் பதிவு ஐக் கோரியுள்ளீர்கள். துரதிர்ஷ்டவசமாக, நீங்கள் வழங்கிய தகவல் எங்கள் அமைப்பில் உள்ள தகவலுடன் பொருந்தவில்லை. {phoneNumber} என்ற எண்ணில் எங்களைத் தொடர்பு கொள்ளவும், உங்கள் பதிவை உங்கள் தொடர்புத் தகவலுடன் பொருத்துவதற்கான உதவிக்கு # ஐ அழுத்தவும்.",
                "hmn" => $"Tsis ntev los no koj tau thov cov ntaub ntawv sau tseg txog kev txheeb xyuas kab mob COVID-19 dis cis tauj los ntawm lub xeev. Hmoov tsis zoo, cov ntaub ntawv uas koj tau muab tsis raug raws li cov ntaub ntawv uas nyob rau hauv peb txheej teg kev ua hauj lwm. Txuas lus rau peb ntawm tus xov tooj {phoneNumber}, nias # rau kev pab ua kom koj cov ntaub ntawv sau tseg raug raws li koj cov ntaub ntawv sib txuas lus.",
                "th" => $"คุณเพิ่งขอบันทึกการยืนยันเกี่ยวกับโควิด-19 แบบดิจิทัลจากรัฐ ขออภัย ข้อมูลที่คุณให้ไม่ตรงกับข้อมูลในระบบของเรา โปรดติดต่อเราที่ {phoneNumber} แล้วกด # เพื่อขอความช่วยเหลือในการจับคู่บันทึกของคุณกับข้อมูลการติดต่อของคุณ",
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
                "es" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Registro digital de verificación de COVID-19</h1>" +
                    $"<p>Recientemente solicitó un Registro digital de verificación de COVID-19 del <a href='{webUrl}'>sistema de Registro digital de verificación de COVID-19</a>. Desafortunadamente, la información que ingresó no coincide con la que tenemos en nuestro sistema estatal.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>Puede presentar otra solicitud en el sistema de <a href='{webUrl}'>Registro digital de verificación de COVID-19</a> con un número de teléfono o dirección de correo electrónico diferente; puede <a href='{contactUsUrl}'>comunicarse con nosotros</a> para que lo ayudemos a fin de que su información de contacto coincida con los registros; o bien, puede comunicarse con su proveedor para asegurarse de que la información ha sido enviada al sistema estatal.</p>" +
                    $"<h2>¿Tiene preguntas?</h2>" +
                    $"<p>Visite nuestra página de <a href='{vaccineFAQUrl}'>preguntas frecuentes</a> para obtener más información sobre el Registro digital de verificación de COVID-19.</p>" +
                    $"<h2>Manténgase informado.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Consulte la información más reciente</a> sobre la COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Correo electrónico oficial del Departamento de Salud del Estado de Washington</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "zh" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>数字 COVID-19 验证记录</h1>" +
                    $"<p>您最近向 <a href='{webUrl}'>数字 COVID-19 验证记录系统</a> 请求过数字 COVID-19 验证记录。很遗憾，您提供的信息与州系统中的信息不符。</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>您可以使用不同的手机号码或电子邮件地址在 <a href='{webUrl}'>数字 COVID-19 验证记录</a> 系统中提交另一个请求，您还可以 <a href='{contactUsUrl}'>联系我们</a> 寻求帮助，将您的记录与您的联系信息进行匹配，或者您可以联系您的医疗保健提供者以确保您的信息已提交至州系统。</p>" +
                    $"<h2>仍有疑问？</h2>" +
                    $"<p>请访问我们的<a href='{vaccineFAQUrl}'>常见问题解答 (FAQ)</a> 页面，以了解有关您的数字 COVID-19 验证记录的更多信息。</p>" +
                    $"<h2>保持关注。</h2>" +
                    $"<p><a href='{covidWebUrl}'>查看 COVID-19 最新信息</a>。</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health （华盛顿州卫生部）官方电子邮件</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "zh-TW" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>數位 COVID-19 驗證記錄</h1>" +
                    $"<p>您最近向 <a href='{webUrl}'>數位 COVID-19 驗證記錄系統</a> 請求過數位 COVID-19 驗證記錄。很遺憾，您提供的資訊與州系統中的資訊不符。</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>您可以使用不同的手機號碼或電子郵件地址在 <a href='{webUrl}'>數位 COVID-19 驗證記錄</a> 系統中提交另一個請求，您還可以 <a href='{contactUsUrl}'>與我們連絡</a> 尋求幫助，將您的記錄與您的連絡資訊進行匹配，或者您可以連絡您的醫療保健提供者以確保您的資訊已提交至州系統。</p>" +
                    $"<h2>仍有疑問？</h2>" +
                    $"<p>請造訪我們的<a href='{vaccineFAQUrl}'>常見問題解答 (FAQ)</a> 頁面，瞭解有關您的數位 COVID-19 驗證記錄的更多資訊。</p>" +
                    $"<h2>保持關注。</h2>" +
                    $"<p><a href='{covidWebUrl}'>檢視最新資訊</a>，與 COVID-19 密切相關的資訊。</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health （華盛頓州衛生部）官方電子郵件</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ko" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>디지털 COVID-19 인증 기록</h1>" +
                    $"<p>귀하는 최근 <a href='{webUrl}'>디지털 COVID-19 인증 기록 시스템</a> 에 디지털 COVID-19 인증 기록을 요청하셨습니다. 유감스럽게도 귀하가 제공하신 정보는 주정부 시스템상 정보와 일치하지 않습니다.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>다른 휴대전화 번호나 이메일 주소로 <a href='{webUrl}'>디지털 COVID-19 인증 기록 시스템</a> 에 별도의 요청을 제출하실 수 있습니다. <a href='{contactUsUrl}'>저희에게 연락</a> 하여 귀하의 기록을 연락처 정보와 일치시키는 데 도움을 받으시거나, 담당 의료서비스 제공자에게 문의하여 귀하의 정보가 주정부 시스템에 제출되었는지 확인하실 수 있습니다.</p>" +
                    $"<h2>궁금한 사항이 있으신가요?</h2>" +
                    $"<p>디지털 COVID-19 인증 기록에 대해 자세히 알아보려면 <a href='{vaccineFAQUrl}'>자주 묻는 질문(FAQ)</a> 페이지를 참조해 주십시오.</p>" +
                    $"<h2>최신 정보를 확인하십시오.</h2>" +
                    $"<p>COVID-19 관련 <a href='{covidWebUrl}'>최신 정보 보기</a></p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health (워싱턴주 보건부) 공식 이메일</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "vi" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Hồ sơ Xác nhận COVID-19 kỹ thuật số</h1>" +
                    $"<p>Gần đây bạn yêu cầu Hồ sơ Xác nhận COVID-19 kỹ thuật số từ <a href='{webUrl}'>hệ thống Hồ sơ Xác nhận COVID-19 kỹ thuật số</a>. Rất tiếc, thông tin mà bạn cung cấp không khớp với thông tin có trong hệ thống của tiểu bang.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>Bạn có thể gửi yêu cầu khác trong hệ thống <a href='{webUrl}'>Hồ sơ Xác nhận COVID-19 kỹ thuật số</a> với một số điện thoại di động hoặc địa chỉ email khác, bạn có thể <a href='{contactUsUrl}'>liên hệ với chúng tôi</a> để được trợ giúp khớp thông tin hồ sơ với thông tin liên lạc của bạn, hoặc bạn có thể liên lạc với nhà cung cấp của mình để đảm bảo rằng thông tin của bạn đã được gửi đến hệ thống của tiểu bang.</p>" +
                    $"<h2>Có câu hỏi?</h2>" +
                    $"<p>Truy cập vào trang <a href='{vaccineFAQUrl}'>Các Câu Hỏi Thường Gặp (FAQ)</a> để tìm hiểu thêm về Hồ Sơ Xác nhận COVID-19 kỹ thuật số của bạn.</p>" +
                    $"<h2>Luôn cập nhật thông tin.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Xem thông tin mới nhất</a> về COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Email chính thức của Washington State Department of Health (Sở Y Tế Tiểu Bang Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ar" => $"<img src='{webUrl}/imgs/waverifylogo.png' dir='rtl' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 dir='rtl' style='color: #C84C0E'>سجل التحقق الرقمي من فيروس كوفيد-19</h1>" +
                    $"<p dir='rtl'>لقد قمت مؤخرًا بطلب الحصول على سجل التحقق الرقمي من فيروس كوفيد-19 من <a href='{webUrl}'>نظام سجل التحقق الرقمي من فيروس كوفيد-19</a> (متوفر باللغة الإنجليزية فقط). ولكن للأسف، المعلومات التي قمت بتقديمها لا تتوافق مع المعلومات الموجودة على نظام الولاية.</p>" +
                    //$"<p dir='rtl'>لقد قمت مؤخرًا بطلب الحصول على سجل التحقق الرقمي من فيروس <a href='{webUrl}'>كوفيد-19 من نظام سجل التحقق الرقمي من فيروس كوفيد-19</a> . ولكن للأسف، المعلومات التي قمت بتقديمها لا تتوافق مع المعلومات الموجودة على نظام الولاية.</p>" +
                    //$"<p dir='rtl'>يمكنك التقدم بطلب آخر في نظام <a href='{webUrl}'>كوفيد-19 من نظام سجل التحقق الرقمي من فيروس كوفيد-19</a> باستخدام رقم هاتف محمول أو بريد إلكتروني مختلف، أو  يمكنك <a href='{contactUsUrl}'>التواصل معنا</a> للحصول على مساعدة في تحقيق التطابق بين سجلك ومعلومات التواصل الخاصة بك، أو يمكنك التواصل مع مُقدِّم الخدمة المعنّي بك للتأكد من إرسال معلوماتك إلى نظام الولاية.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p dir='rtl'>يمكنك التقدم بطلب آخر في نظام <a href='{webUrl}'>سجل التحقق الرقمي من فيروس كوفيد-19</a> (متوفر باللغة الإنجليزية فقط) باستخدام رقم هاتف محمول أو بريد إلكتروني مختلف، أو يمكنك <a href='{contactUsUrl}'>التواصل معنا</a> (متوفر باللغة الإنجليزية فقط) للحصول على مساعدة في تحقيق التطابق بين سجلك ومعلومات التواصل الخاصة بك، أو يمكنك التواصل مع مُقدِّم الخدمة المعنّي بك للتأكد من إرسال معلوماتك إلى نظام الولاية.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<h2 dir='rtl'>هل لديك أي أسئلة؟ </h2>" +
                    $"<p dir='rtl'>قم بزيارة صفحة <a href='{vaccineFAQUrl}'>الأسئلة الشائعة</a> (متوفر باللغة الإنجليزية فقط) الخاصة بنا للاطلاع على مزيدٍ من المعلومات حول السجل الرقمي للقاح كوفيد-19 الخاص بك.</p>" +
                    $"<h2 dir='rtl'>ابقَ مطلعًا.</h2>" +
                    $"<p dir='rtl'><a href='{covidWebUrl}'>عرض آخر المعلومات </a> (متوفر باللغة الإنجليزية فقط) عن فيروس كوفيد-19.</p>" +

                    $"<hr>" +
                    $"<footer><p dir='rtl' style='text-align:center'>البريد الإلكتروني الرسمي الخاص بـ Washington State Department of Health (إدارة الصحة في ولاية واشنطن)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "tl" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Digital na Rekord sa Pagberipika ng Pagpapabakuna sa COVID-19</h1>" +
                    $"<p>Kamakailan kang humiling ng Digital na Rekord sa Pagberipika ng Pagpapabakuna sa COVID-19 mula <a href='{webUrl}'>system ng Digital na Rekord sa Pagberipika ng Pagpapabakuna sa COVID-19</a>. Sa kasamaang-palad, hindi tumutugma ang ibinigay mong impormasyon sa impormasyong nasa system ng estado.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>Maaari kang magsumite sa isa pang kahilingan sa system ng <a href='{webUrl}'>Digital na Rekord sa Pagberipika ng Pagpapabakuna sa COVID-19</a> gamit ang ibang numero ng mobile na telepono o email address, <a href='{contactUsUrl}'>makipag-ugnayan sa amin</a> para sa tulong sa pagtugma ng iyong rekord sa impormasyon sa pakikipag-ugnayan mo, o makipag-ugnayan sa iyong provider para tiyaking isinumite sa system ng estado ang iyong impormasyon.</p>" +
                    $"<h2>May mga tanong?</h2>" +
                    $"<p>Bisitahin ang aming page ng <a href='{vaccineFAQUrl}'>Mga Madalas Itanong (FAQ)</a> para matuto pa tungkol sa iyong Digital na Rekord sa Pagberipika ng Pagpapabakuna sa COVID-19.</p>" +
                    $"<h2>Manatiling May Kaalaman.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Tingnan ang pinakabagong impormasyon</a> tungkol sa COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Opisyal na Email ng Washington State Department of Health (Departamento ng Kalusugan ng Estado ng Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ru" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Цифровая запись о вакцинации от COVID-19</h1>" +
                    $"<p>Недавно вы запросили цифровую запись о вакцинации от COVID-19 в <a href='{webUrl}'>системе штата</a>. К сожалению, предоставленная вами информация не совпадает с информацией в системе штата.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>Вы можете подать еще один запрос в <a href='{webUrl}'>системе цифровых записей о вакцинации от COVID-19</a>, используя другой номер мобильного телефона или адрес электронной почты, а также <a href='{contactUsUrl}'>связаться с нами</a> , чтобы получить помощь в сверке данных, указанных в вашей записи, с вашей контактной информацией, или обратиться к своему лечащему врачу, чтобы убедиться, что ваша информация была внесена в систему штата.</p>" +
                    $"<h2>Возникли вопросы?</h2>" +
                    $"<p>Чтобы узнать больше о цифровой записи о вакцинации от COVID-19, перейдите на нашу страницу <a href='{vaccineFAQUrl}'>«Часто задаваемые вопросы» (FAQ)</a>.</p>" +
                    $"<h2>Оставайтесь в курсе.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Получайте актуальную информацию</a> о COVID-19 .</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Официальный адрес электронной почты Washington State Department of Health (Департамент здравоохранения штата Вашингтон)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ja" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>COVID-19ワクチン接種電子記録</h1>" +
                    $"<p>あなたは<a href='{webUrl}'>COVID-19ワクチン接種電子記録システム</a>からCOVID-19ワクチン接種電子記録を依頼されましたが、入力された情報はワシントン州のシステムに登録されている情報と一致しません。</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>別の携帯電話番号または電子メールアドレスを使用して<a href='{webUrl}'>コロナワクチン接種電子記録</a>システムにあらためて依頼を送信できます。あなたの記録と連絡先情報を一致させる上でサポートが必要な場合は<a href='{contactUsUrl}'>問い合わせください</a>。またはワクチン提供機関に連絡して、あなたの情報がワシントン州のシステムに送信済みであるか確認できます。</p>" +
                    $"<h2>ご不明な点がありますか？</h2>" +
                    $"<p>COVID-19ワクチン接種電子記録についての詳細は、<a href='{vaccineFAQUrl}'>よくある質問（FAQ)</a>ページをご覧ください。</p>" +
                    $"<h2>最新の情報を入手する </h2>" +
                    $"<p>新型コロナ感染症について<a href='{covidWebUrl}'>最新情報を見る</a>。</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health（ワシントン州保健局）の公式電子メール</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "fr" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Attestation numérique de vaccination COVID-19</h1>" +
                    $"<p>Vous avez récemment demandé une Attestation numérique de vaccination COVID-19 auprès du système d'<a href='{webUrl}'>Attestation numérique de vaccination COVID-19</a>. Malheureusement, les informations que vous avez fournies ne correspondent pas à celles qui figurent dans le système de l'État.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>Vous pouvez soumettre une autre demande dans le système d'<a href='{webUrl}'>Attestation numérique de vaccination COVID-19</a> en indiquant un autre numéro de téléphone mobile ou une autre adresse e-mail, vous pouvez <a href='{contactUsUrl}'>nous contacter</a> pour obtenir de l'aide afin d'associer votre attestation à vos coordonnées, ou vous pouvez contacter votre professionnel de santé pour vérifier que vos informations ont été transmises au système de l'État.</p>" +
                    $"<h2>Vous avez des questions?</h2>" +
                    $"<p>Consultez notre page <a href='{vaccineFAQUrl}'>Foire Aux Questions (FAQ)</a> pour en savoir plus sur votre Attestation numérique de vaccination COVID-19.</p>" +                    
                    $"<h2>Informez-vous.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Voir les dernières informations</a> à propos du COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>E-mail officiel du Washington State Department of Health (ministère de la Santé de l'État de Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "tr" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Dijital COVID-19 Doğrulama Kaydı</h1>" +
                    $"<p>Yakın zamanda <a href='{webUrl}'>Dijital COVID-19 Doğrulama Kaydı sisteminden</a> bir Dijital COVID-19 Doğrulama Kaydı istediniz. Maalesef verdiğiniz bilgiler eyalet sistemindeki bilgilerle eşleşmiyor.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p><a href='{webUrl}'>Dijital COVID-19 Doğrulama Kaydı</a> sisteminden farklı bir cep telefonu numarası ya da e-posta adresi ile başka bir istekte bulabilir, kaydınızı iletişim bilgilerinizle eşleştirme konusunda yardım almak için <a href='{contactUsUrl}'>bize ulaşabilir</a> ya da bilgilerinizin eyalet sistemine gönderildiğinden emin olmak için sağlayıcınızla iletişime geçebilirsiniz.</p>" +
                    $"<h2>Sorularınız mı var?</h2>" +
                    $"<p>Dijital COVID-19 Doğrulama Kaydı’nız hakkında daha fazla bilgi almak için <a href='{vaccineFAQUrl}'>Sıkça Sorulan Sorular (SSS)</a> bölümümüzü ziyaret edin.</p>" +
                    $"<h2>Güncel bilgilere sahip olun.</h2>" +
                    $"<p>COVID-19 <a href='{covidWebUrl}'>hakkında en güncel bilgileri görüntüleyin</a>.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Resmi Washington State Department of Health (Washington Eyaleti Sağlık Bakanlığı) E-postası</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "uk" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                   $"<h1 style='color: #C84C0E'>Електронний запис про підтвердження вакцинації від COVID-19</h1>" +
                   $"<p>Нещодавно ви надіслали запит на отримання доступу до електронного запису про підтвердження вакцинації від COVID-19 у <a href='{webUrl}'>системі «Електронний запис про підтвердження вакцинації від COVID-19»</a>. На жаль, інформація, яку ви надали, не збігається з інформацією в державній системі.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                   $"<p>Ви можете подати інший запит у системі <a href='{webUrl}'>Електронний запис про підтвердження вакцинації від COVID-19</a>, використовуючи інший номер мобільного телефону або адресу електронної пошти, <a href='{contactUsUrl}'>зв’язатися з нами</a>, щоб отримати допомогу в зіставленні інформації у вашому записі з вашою контактною інформацією, або звернутися до свого лікаря, щоб переконатися, що вашу інформацію передано до державної системи.</p>" +
                   $"<h2>Маєте запитання?</h2>" +
                   $"<p>Щоб дізнатися більше про систему «Електронний запис про підтвердження вакцинації від COVID-19», перегляньте розділ <a href='{vaccineFAQUrl}'>Найпоширеніші запитання</a>.</p>" +
                   $"<h2>Будьте в курсі.</h2>" +
                   $"<p><a href='{covidWebUrl}'>Перегляньте найновішу інформацію</a> про COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                   $"<hr>" +
                   $"<footer><p style='text-align:center'>Офіційна електронна адреса Washington State Department of Health (Департаменту охорони здоров’я штату Вашингтон)</p>" +
                   $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ro" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Certificatul digital COVID-19</h1>" +
                    $"<p>Ați solicitat recent un certificat digital COVID-19 de la <a href='{webUrl}'>sistemul de certificate digitale COVID-19</a>. Din păcate, informațiile furnizate de dvs. nu corespund cu datele din sistemul de stat.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>Puteți să trimiteți o nouă solicitare către sistemul de <a href='{webUrl}'>certificate digitale COVID-19</a> de la un alt număr de telefon mobil sau de pe o altă adresă de e-mail, puteți să ne <a href='{contactUsUrl}'>contactați</a> pentru asistență la potrivirea fișei dvs. de vaccinare cu informațiile de contact sau puteți să luați legătura cu furnizorul serviciilor medicale pentru a vă asigura ca datele dvs. au fost introduse în sistemul de stat.</p>" +
                    $"<h2>Aveți întrebări?</h2>" +
                    $"<p>Accesați pagina Întrebări frecvente (<a href='{vaccineFAQUrl}'>Întrebări frecvente</a>) pentru a afla mai multe despre certificatul digital COVID-19.</p>" +
                    $"<h2>Rămâneți informat.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Vizualizați cele mai recente informații</a> referitoare la COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<footer><p style='text-align:center'>Adresa de e-mail oficială a Washington State Department of Health (Departamentului de Sănătate al Statului Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "pt" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Comprovante digital de vacinação contra a COVID-19</h1>" +
                    $"<p>Recentemente, você solicitou um Comprovante digital de vacinação contra a COVID-19 do <a href='{webUrl}'>sistema de Comprovante digital de vacinação contra a COVID-19</a>. Infelizmente, as informações fornecidas não correspondem às informações em nosso sistema.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>É possível enviar outra solicitação ao sistema de <a href='{webUrl}'>Comprovante digital de vacinação contra a COVID-19</a> com um número de celular ou endereço de e-mail diferente. <a href='{contactUsUrl}'>Entre em contato conosco</a> para obter ajuda com a correspondência entre os dados de contato e as informações em seu comprovante, ou entre em contato com o seu provedor para garantir que suas informações foram enviadas para o sistema do estado.</p>" +
                    $"<h2>Tem dúvidas?</h2>" +
                    $"<p>Visite a nossa página de <a href='{vaccineFAQUrl}'>Perguntas frequentes (FAQ)</a> para saber mais sobre o seu Comprovante digital de vacinação contra a COVID-19.</p>" +
                    $"<h2>Mantenha-se informado.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Veja as informações mais recentes</a> sobre a COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>E-mail do representante oficial do Washington State Department of Health (Departamento de Saúde do estado de Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "hi" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>डिजिटल COVID-19 सत्यापन रिकॉर्ड</h1>" +
                    $"<p>आपने हाल ही में <a href='{webUrl}'>डिजिटल COVID-19 सत्यापन रिकॉर्ड प्रणाली </a> से डिजिटल COVID-19 सत्यापन रिकॉर्ड का अनुरोध किया है। दुर्भाग्य से, आपके द्वारा प्रदान की गई जानकारी राज्य प्रणाली की जानकारी से मेल नहीं खाती है। </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>आप एक अलग मोबाइल फोन नंबर या ईमेल एड्रेस के साथ <a href='{webUrl}'>डिजिटल COVID-19 सत्यापन रिकॉर्ड</a> प्रणाली में एक और अनुरोध सबमिट कर सकते हैं, आप अपनी संपर्क जानकारी को अपने रिकॉर्ड से मिलान करने में मदद के लिए <a href='{contactUsUrl}'>हमसे संपर्क कर सकते हैं </a>, या आप यह सुनिश्चित करने के लिए अपने प्रदाता से संपर्क कर सकते हैं कि आपकी जानकारी राज्य प्रणाली को प्रस्तुत की गई है या नहीं।</p>" +
                    $"<h2>आपके कोई प्रश्न हैं?</h2>" +
                    $"<p>अपने डिजिटल COVID-19 वेरिफिकेशन रिकॉर्ड के बारे में अधिक जानने के लिए हमारे <a href='{vaccineFAQUrl}'>अक्सर पूछे जाने वाले प्रश्न (FAQ)</a> पृष्ठ पर जाएँ।</p>" +
                    $"<h2>सूचित रहें।</h2>" +
                    $"<p>COVID-19 के बारे में <a href='{covidWebUrl}'>नवीनतम जानकारी देखें</a>। </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health (वाशिंगटन राज्य के स्वास्थ्य विभाग) का आधिकारिक ईमेल</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "de" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>COVID-19-Digitalzertifikat</h1>" +
                    $"<p>Sie haben kürzlich ein COVID-19-Digitalzertifikat vom <a href='{webUrl}'>COVID-19-Digitalzertifikat-System</a> angefordert. Leider stimmen die von Ihnen gemachten Angaben nicht mit den Informationen im System des Bundesstaats überein.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>Sie können im <a href='{webUrl}'>COVID-19-Digitalzertifikat</a>-System eine erneute Anfrage mit einer anderen Handynummer oder E-Mail-Adresse senden, Sie können <a href='{contactUsUrl}'>Kontakt</a> zu uns aufnehmen, damit wir Ihnen bei der Zuordnung Ihres Zertifikats zu Ihren Kontaktdaten helfen, oder Sie können Ihren Anbieter kontaktieren, sich zu vergewissern, dass Ihre Daten an das System des Bundesstaats übermittelt wurden.</p>" +
                    $"<h2>Haben Sie Fragen?</h2>" +
                    $"<p>Besuchen Sie unsere Seite mit <a href='{vaccineFAQUrl}'>häufig gestellten Fragen (FAQ)</a>, um mehr über Ihr COVID-19-Digitalzertifikat zu erfahren.</p>" +
                    $"<h2>Bleiben Sie auf dem Laufenden.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Sehen Sie sich die neuesten Informationen</a> über COVID-19 an.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Offizielle E-Mail-Adresse des Washington State Department of Health (Gesundheitsministerium des Bundesstaates Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ti" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>ዲጂታላዊ ናይ ኮቪድ-19 ክታበት መረጋገጺ መዝገብ</h1>" +
                    $"<p>ንስኹም ኣብ ቀረባ ግዜ ካብ <a href='{webUrl}'>ስርዓተ ዲጂታላዊ ናይ ኮቪድ-19 መረጋገጺ መዝገብ</a> ዲጂታላዊ ናይ ኮቪድ-19 ክታበት መረጋገጺ መዝገብ ሓቲትኩም። ኣጋጣሚ ኮይኑግን፡ እቲ ንስኹም ዝሃብክምዎ ሓበሬታ ከኣ ምስ’ቲ ኣብ ሲስተምና ዘሎ ሓበሬታ ኣይተሰማመዐን።</p>" +
                    $"<p><a href='{webUrl}'>ካልእ ሞባይል ቴለፎን ወይ ናይ ኢመይል ኣድራሻ ኣብዲጂታላዊ ናይ ኮቪድ-19 መረጋገጺ መዝገብ ሲስተም</a> ካልእ ሕቶ ከተእትዉ ትኽእሉ ኢኹም፡ ንሓገዝዛብ ምዝማድ ናትኩም መዝገብ ምስ ናይ መወከሲ ሓበሬታኹም ድማ ንስኹም <a href='{contactUsUrl}'>ክትዉከሱና</a> ትኽእሉ ኢኹም፡ ወይ ንኣዳላዊኹም ሓበሬታኹም ናብ ናይ ስተይት ሲስተም ከምዝኣተወ ንምርግጋጽ ክትውከስዎ ትኽእሉ ኢኹም።</p>" +
                    $"<h2>ሕቶታት ኣለኩም ድዩ?</h2>" +
                    $"<p>ብዛዕባ ዲጂታላዊ ናይ ኮቪድ-19 ክታበት መረጋገጺ መዝገብ ዝያዳ ንምፍላጥ፡ ነቶም ናህና ቀጻሊ ዝሕተቱ ሕቶታት <a href='{vaccineFAQUrl}'>ቀጻሊ ዝሕተቱ ሕቶታት</a> ብጽሑ</h2>" +
                    $"<h2>ሓበሬታ ሓዙ።</h2>" +
                    $"<p>ብዛዕባ ኮቪድ-19 <a href='{covidWebUrl}'>ናይ ቀረባ ግዜ ሓበሬታ ራኣዩ</a> ኢኹም።</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>ወግዓዊ ናይ Washington State Department of Health (ክፍሊ ጥዕና ግዝኣት ዋሽንግተን) ኢ-መይል</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "te" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>డిజిటల్ కొవిడ్-19 ధృవీకరణ రికార్డ్</h1>" +
                    $"<p>మీరు ఇటీవల <a href='{webUrl}'>డిజిటల్ కొవిడ్-19 ధృవీకరణ రికార్డ్ సిస్టమ్ నుంచ</a> డిజిటల్ కొవిడ్-19 ధృవీకరణ రికార్డ్​ని అభ్యర్ధించారు. దురదృష్టవశాత్తు, మీరు అందించిన సమాచారం స్టేట్ సిస్టమ్​లో సమాచారంతో జతకావడం లేదు.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>మీరు వేరే మొబైల్ నెంబరు లేదా ఇమెయిల్ చిరునామాతో <a href='{webUrl}'>డిజిటల్ కొవిడ్-19 ధృవీకరణ రికార్డు</a> సిస్టమ్​లో మరో అభ్యర్ధన సబ్మిట్ చేయవచ్చు, మీరు మీ రికార్డులను మీ కాంటాక్ట్ సమాచారంతో జత చేయడానికి <a href='{contactUsUrl}'>మమ్మల్ని సంప్రదించవచ్చు</a>, లేదా మీ సమాచారం రాష్ట్ర సిస్టమ్​కు సబ్మిట్ చేసినట్లుగా ధృవీకరించుకోవడానికి మీరు మీ ప్రొవైడర్​ని సంప్రదించవచ్చు.</p>" +
                    $"<h2>మీకు ఏమైనా ప్రశ్నలున్నాయా?</h2>" +
                    $"<p>డిజిటల్ కొవిడ్-19 ధృవీకరణ రికార్డ్ గురించి మరింత తెలుసుకోవడానికి మా <a href='{vaccineFAQUrl}'>తరచుగా అడిగే ప్రశ్నలు (FAQ)</a> పేజీని సందర్శించండి.</p>" +
                    $"<h2>అవగాహనతో ఉండండి.</h2>" +
                    $"<p>కొవిడ్-19పై <a href='{covidWebUrl}'>తాజా సమాచారాన్ని వీక్షించండి</a>.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>అధికారిక Washington State Department of Health (వాషింగ్టన్ స్టేట్ డిపార్ట్​మెంట్ ఆఫ్ హెల్త్) ఇమెయిల్</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "sw" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Rekodi ya Kidijitali ya Uthibitishaji wa COVID-19</h1>" +
                    $"<p>Hivi karibuni uliomba Rekodi ya Kidijitali ya Uthibitishaji wa COVID-19 kutoka mfumo wa <a href='{webUrl}'>Rekodi ya Kidijitali ya Uthibitishaji wa COVID-19</a>. Kwa bahati mbaya, maelezo uliyotoa hayalingani na maelezo yaliyopo kwenye mfumo wa jimbo.</a></p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>Unaweza kuwasilisha ombi lingine katika mfumo wa <a href='{webUrl}'>Rekodi ya Kidijitali ya Uthibitishaji wa COVID-19</a> kwa nambari tofauti ya simu ya mkononi au barua pepe, unaweza <a href='{contactUsUrl}'>kuwasiliana nasi</a> ili kupata usaidizi katika kulinganisha rekodi yako na maelezo yako ya mawasiliano, au unaweza kuwasiliana na mtoaji wako ili kuhakikisha maelezo yako yamewasilishwa kwa mfumo wa jimbo.</a></a></p>" +
                    $"<h2>Una maswali?</h2>" +
                    $"<p>Tembelea ukurasa wa <a href='{vaccineFAQUrl}'>Maswali yetu Yanayoulizwa Mara kwa Mara (FAQ)</a> ili kujifunza zaidi kuhusu Rekodi yako ya Kidijitali ya Uthibitishaji wa COVID-19.</p>" +
                    $"<h2>Endelea Kupata Habari.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Tazama maelezo ya hivi karibuni</a> kuhusu COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Barua pepe Rasmi ya Washington State Department of Health (Idara ya Afya katika Jimbo la Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "so" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Diiwaanka Xaqiijinta Tallaalka COVID-19 ee Dhijitaalka Ah</h1>" +
                    $"<p>Waxaad dhawaan ka codsatay Diiwaanka Xaqiijinta Tallaalka COVID-19 ee Dhijitaalka ah <a href='{webUrl}'>Nidaamka Diiwaanka Xaqiijinta Tallaalka COVID-19 ee Dhijitaalka Ah</a> . Nasiib daro, macluumaadka aad bixisay ma waafaqsana macluumaadka kujira nidaamka gobolka. </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>Waxaad codsi kale ku gudbin kartaa nidaamka <a href='{webUrl}'>Diiwaanka Xaqiijinta Tallaalka COVID-19 ee Dhijitaalka Ah</a>  taleefoon lambar ama ciwaanka iimeel kale, <a href='{contactUsUrl}'>nalasoo xiriir</a>  si aad uhesho caawimaada islahaysiinta diiwaankaaga iyo macluumaadkaaga xiriirka, ama waxaad la xariiri kartaa bixiyahaaga tallaalka si loo xaqiijiyo in macluumaadkaada loo gudbiyey nidaamka gobolka.</p>" +
                    $"<h2>Su'aalo ma qabtaa?</h2>" +
                    $"<p>Booqo boggeena (<a href='{vaccineFAQUrl}'>Su'aalaha Badanaa La Iswaydiiyo</a>)  si aad wax badan uga ogaato Diiwaankaaga Xaqiijinta Tallaalka COVID-19 ee Dhijitaalka ah.</p>" +
                    $"<h2>La Soco Xogta.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Arag Macluumaadki ugu danbeeyey</a>  oo ku saabsan COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Iimeelka Rasmiga Ee Washington State Department of Health (Waaxda Caafimaadka Gobolka Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "sm" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Faamaumauga Faamaonia o le KOVITI-19</h1>" +
                    $"<p>Sa e talosagaina talu ai nei ni faamaumauga faamaonia o le KOVITI-19 <a href='{webUrl}'>Polokalame o faamaumauga o le KOVITI-19</a>. E faamalie atu, o faamatalaga na e tuuina mai, e le tutusa ma faamaumauga i le polokalame a le setete </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>E mafai ona toe talosagaina ni <a href='{webUrl}'>Faamaumauga Faamaonia o le KOVITI-19</a> e manaomia se numera telefoni feaveai ese, poo se tuatusi imeli, e mafai foi <a href='{contactUsUrl}'>faafesootai mai matou</a> mo se fesoasoani e faamautinoa ou faaamaumauga pe faafesootai le auaunaga ina ia faamautuina ua mauaina uma ou faamatalaga i polokalame a le setete.</p>" +
                    $"<h2>E iai ni fesili?</h2>" +
                    $"<p>Asiasi ane i mataupu e masani ona fesiligia (<a href='{vaccineFAQUrl}'>Fesili Masani ma Tali</a>) e faalauteleina ai lou silafia i Fa’amaumauga Fa’amaonia o le KOVITI-19 i luga o Upega Tafa’ilagi.</p>" +
                    $"<h2>Ia silafia Pea.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Silasila i faamatalaga lata mai</a> o le KOVITI-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Imeli aloaia a le Washington State Department of Health (Matagaluega o le Soifua Maloloina a le Setete o Uosigitone)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "pa" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>ਡਿਜੀਟਲ ਕੋਵਿਡ-19 ਵੇਰਿਫਿਕੇਸ਼ਨ ਰਿਕਾਰਡ</h1>" +
                    $"<p>ਤੁਸੀਂ ਹਾਲ ਹੀ ਵਿੱਚ <a href='{webUrl}'>ਡਿਜੀਟਲ ਕੋਵਿਡ-19 ਵੇਰਿਫਿਕੇਸ਼ਨ ਰਿਕਾਰਡ ਸਿਸਟਮ</a> ਤੋਂ ਇੱਕ ਡਿਜੀਟਲ ਕੋਵਿਡ-19 ਵੇਰਿਫਿਕੇਸ਼ਨ ਰਿਕਾਰਡ ਲਈ ਬੇਨਤੀ ਕੀਤੀ ਸੀ। ਬਦਕਿਸਮਤੀ ਨਾਲ, ਤੁਹਾਡੇ ਦੁਆਰਾ ਪ੍ਰਦਾਨ ਕੀਤੀ ਗਈ ਜਾਣਕਾਰੀ ਸਟੇਟ ਦੇ ਸਿਸਟਮ ਵਿੱਚ ਮੌਜੂਦ ਜਾਣਕਾਰੀ ਨਾਲ ਮੇਲ ਨਹੀਂ ਖਾਂਦੀ। </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>ਤੁਸੀਂ ਕਿਸੇ ਵੱਖਰੇ ਮੋਬਾਈਲ ਨੰਬਰ ਜਾਂ ਈਮੇਲ ਪਤੇ ਨਾਲ <a href='{webUrl}'>ਡਿਜੀਟਲ ਕੋਵਿਡ-19 ਵੇਰਿਫਿਕੇਸ਼ਨ ਰਿਕਾਰਡ</a>  ਸਿਸਟਮ ਵਿੱਚ ਇੱਕ ਹੋਰ ਬੇਨਤੀ ਸਬਮਿਟ ਕਰ ਸਕਦੇ ਹੋ, ਆਪਣੇ ਰਿਕਾਰਡ ਨੂੰ ਆਪਣੀ ਸੰਪਰਕ ਜਾਣਕਾਰੀ ਨਾਲ ਮਿਲਾਉਣ ਵਿੱਚ ਮਦਦ ਲਈ ਤੁਸੀਂ <a href='{contactUsUrl}'>ਸਾਡੇ ਨਾਲ ਸੰਪਰਕ ਕਰ</a>  ਸਕਦੇ ਹੋ, ਜਾਂ ਇਹ ਯਕੀਨੀ ਬਣਾਉਣ ਲਈ ਤੁਸੀਂ ਆਪਣੇ ਸਿਹਤ ਸੰਭਾਲ ਪ੍ਰਦਾਤਾ ਨਾਲ ਸੰਪਰਕ ਕਰ ਸਕਦੇ ਹੋ ਕਿ ਤੁਹਾਡੀ ਜਾਣਕਾਰੀ ਸਟੇਟ ਦੇ ਸਿਸਟਮ ਵਿੱਚ ਸਬਮਿਟ ਕਰ ਦਿੱਤੀ ਗਈ ਹੈ।</p>" +
                    $"<h2>ਕੀ ਤੁਹਾਡੇ ਕੋਈ ਸਵਾਲ ਹਨ?</h2>" +
                    $"<p>ਆਪਣੇ ਡਿਜ਼ੀਟਲ ਕੋਵਿਡ-19 ਵੈਰੀਫਿਕੇਸ਼ਨ ਰਿਕਾਰਡ ਬਾਰੇ ਹੋਰ ਜਾਣਨ ਲਈ ਸਾਡੇ <a href='{vaccineFAQUrl}'>ਅਕਸਰ ਪੁੱਛੇ ਜਾਣ ਵਾਲੇ ਸਵਾਲ (FAQ)</a></p>" +
                    $"<h2>ਸੂਚਿਤ ਰਹੋ।</h2>" +
                    $"<p>ਕੋਵਿਡ-19 ਬਾਰੇ <a href='{covidWebUrl}'>ਨਵੀਨਤਮ ਜਾਣਕਾਰੀ ਵੇਖੋ</a></p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health (ਵਾਸ਼ਿੰਗਟਨ ਸਟੇਟ ਸਿਹਤ ਵਿਭਾਗ) ਦਾ ਅਧਿਕਾਰਤ ਈਮੇਲ ਪਤਾ</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ps" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 dir='rtl' style='color: #C84C0E'>د ډیجیټل COVID-19 تائید ثبت</h1>" +
                    $"<p dir='rtl'>تاسو پدې وروستیو کې <a href='{webUrl}'>د ډیجیټل COVID-19 تائید ثبت سیسټم څخه د ډیجیټل COVID-19 تائید ثبت غوښتنه کړې</a>. له بده مرغه، هغه معلومات چې تاسو چمتو کړي د دولتي سیسټم کې د معلوماتو سره سمون نلري. </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p dir='rtl'>تاسو کولی شئ د <a href='{webUrl}'>ډیجیټل COVID-19 تائید ثبت</a> سیسټم کې د مختلف ګرځنده تلیفون شمیرې یا بریښنالیک ادرس سره بله غوښتنه وسپارئ، تاسو کولی شي خپل ثبت د خپل اړیکې معلوماتو سره د سمولو مرستې لپاره <a href='{contactUsUrl}'>زموږ سره اړیکه ونیسئ</a> ، یا تاسو کولی شئ له خپل چمتو کونکي سره اړیکه ونیسئ ترڅو ډاډمن شي چې ستاسو معلومات دولتي سیسټم ته سپارل شوي دي.</p>" +
                    $"<h2 dir='rtl'>ایا پوښتنې لرئ؟</h2>" +
                    $"<p dir='rtl'>د خپل ډیجیټل واکسین ثبت په اړه د نورې زده کړې لپاره زموږ په COVID-19 <a href='{vaccineFAQUrl}'>مکرر ډول پوښتل شوي پوښتنو (FAQ)</a> پاڼې څخه لیدنه وکړئ.</p>" +
                    $"<h2 dir='rtl'>باخبر اوسئ.</h2>" +
                    $"<p dir='rtl'>دCOVID-19 په<a href='{covidWebUrl}'>اړه تازه معلومات وګورئ</a>.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p dir='rtl' style='text-align:center'>د واشنګټن ایالت د روغتیا ریاست (Washington State Department of Health) رسمي بریښنالیک</p>" +
                    $"<p dir='rtl' style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ur" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 dir='rtl' style='color: #C84C0E'>ڈیجیٹل کووڈ-19 تصدیقی ریکارڈ</h1>" +
                    $"<p dir='rtl'>آپ نے حال ہی میں <a href='{webUrl}'>ڈیجیٹل کووڈ-19 تصدیقی ریکارڈ سسٹم</a> سے ڈیجیٹل کووڈ-19 تصدیقی ریکارڈ کی درخواست کی ہے۔ بدقسمتی سے آپ کی فراہم کردہ معلومات ریاستی سسٹم میں موجود معلومات سے مماثلت نہیں رکھتی۔ </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p dir='rtl'>آپ کوئی اور موبائل فون نمبر یا ای میل ایڈریس استعمال کرتے ہوئے <a href='{webUrl}'>ڈیجیٹل کووڈ-19 تصدیقی ریکارڈ</a> سسٹم میں دوبارہ درخواست جمع کروا سکتے ہیں، اپنے ریکارڈ کو اپنے رابطے کی معلومات سے ملانے کے لئے <a href='{contactUsUrl}'>ہم سے رابطہ</a> کر سکتے ہیں، یا اپنے معالج سے رابطہ کر کے یقینی بنا سکتے ہیں کہ آپ کی معلومات ریاستی نظام میں جمع کروا دی گئی ہیں۔</p>" +
                    $"<h2 dir='rtl'>سوالات ہیں؟</h2>" +
                    $"<p dir='rtl'>اپنے ڈیجیٹل کووڈ-19 تصدیقی ریکارڈ کے متعلق مزید جاننے کے لئے ہمارا <a href='{vaccineFAQUrl}'>عمومی سوالات (FAQ)</a> کا صفحہ ملاحظہ کریں۔</p>" +
                    $"<h2 dir='rtl'>آگاہ رہیں۔</h2>" +
                    $"<p dir='rtl'>کووڈ-19 کے متعلق <a href='{covidWebUrl}'>تازہ ترین معلومات دیکھیں</a></p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p dir='rtl' style='text-align:center'>Washington State Department of Health (DOH، ریاست واشنگٹن محکمۂ صحت) کی سرکاری ای میل</p>" +
                    $"<p dir='rtl' style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ne" =>
                    $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>डिजिटल कोभिड-19 प्रमाणीकरण रेकर्ड</h1>" +
                    $"<p>तपाईंले हालै <a href='{webUrl}'>डिजिटल कोभिड-19 प्रमाणीकरण रेकर्ड प्रणाली</a> बाट डिजिटल कोभिड-19 प्रमाणीकरण रेकर्डको लागि अनुरोध गर्नुभयो। दुर्भाग्यवश, तपाईंले उपलब्ध गराउनुभएको जानकारी राज्यको प्रणालीमा भएको जानकारीसँग मेल खाँदैन। </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>तपाईं भिन्न मोबाइल फोन नम्बर वा इमेल ठेगाना प्रयोग गरी <a href='{webUrl}'>डिजिटल कोभिड-19 प्रमाणीकरण रेकर्ड</a> प्रणालीमा अर्को अनुरोध पेश गर्न सक्नुहुन्छ, तपाईंको रेकर्डलाई तपाईंको सम्पर्क जानकारीसँग मिल्ने बनाउनमा मद्दतका लागि <a href='{contactUsUrl}'>हामीलाई सम्पर्क गर्न</a> सक्नुहुन्छ वा तपाईंको जानकारी राज्यको प्रणालीमा पेश गरिएको छ भनी सुनिश्चित गर्नका लागि आफ्नो प्रदायकलाई सम्पर्क गर्न सक्नुहुन्छ।</p>" +
                    $"<h2>प्रश्नहरू छन्?</h2>" +
                    $"<p>आफ्नो डिजिटल कोभिड-19 प्रमाणीकरण रेकर्डका बारेमा थप जान्नका लागि हाम्रो <a href='{vaccineFAQUrl}'>बारम्बार सोधिने प्रश्नहरू (FAQ)</a> को पृष्ठ हेर्नुहोस्।</p>" +
                    $"<h2>सूचित रहनुहोस्।</h2>" +
                    $"<p>कोभिड-19 बारे <a href='{covidWebUrl}'>नवीनतम जानकारी हेर्नुहोस्</a> ।</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>आधिकारिक Washington State Department of Health(वासिङ्गटन राज्यको स्वास्थ्य विभाग) को इमेल</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "mxb" =>
                    $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Tutu nuu kaa ndichí siki tu’un nasa iyo ní jín kue’e COVID-19</h1>" +
                    $"<p>Iyo jaku kivi ja ni jikan ní in tutu nuu kaa ndichí siki tu’un nasa iyo ní jín kue’e COVID-19 ja iyo nuu <a href='{webUrl}'>Tutu nuu kaa ndichí siki tu’un nasa iyo ní jín kue’e COVID-19</a>. Kueka kuu ja, tu’un ja ni taji ní nduu kitan ji tu’un ja neva’a sa nuu nda tu’un ja iyo nuu ñuu. </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>Kuu tetiñu ní tuku ní inka tutu nuu <a href='{webUrl}'>Tutu nuu kaa ndichí siki tu’un nasa iyo ní jín kue’e COVID-19</a> jín in número yokaa axi correo electrónico ja síin kaa, kuu <a href='{contactUsUrl}'>ka’an ní jín nda sa</a> tágua kuu sa’a yo ja kita’an tu’un ja taji ní ji ja neva’a sa, axi in ñayiví satatan tágua kuni ní tú ni tetiñu va’a tu’un siki ní nuu ñuu.</p>" +
                    $"<h2>A iyo tu’un jikatu’un ní</h2>" +
                    $"<p>Kunde’e ní nuu página <a href='{vaccineFAQUrl}'>nda tu’un jikatu’un ka (FAQ)</a> tágua ni’in ka ní tu’un siki nasa chi’in ni sivi ní nuu Tutu nuu Kaa ndichí siki Tu’un Nasa iyo ní jín kue’e COVID-19.</p>" +
                    $"<h2>Ndukú ni tu’un.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Kunde’e ní tu’un jáá ka</a> siki COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Correo Washington State Department of Health (DOH, Ve’e nuu jito ja Sa’a tátan ñuu Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "mh" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Iaam jarom COVID-19 Jaak Jeje</h1>" +
                    $"<p>Eok kiin jeṃaanḷọk kajjitōk a laam jarom COVID-19 jaak jeje jan <a href='{webUrl}'>ko Kaajai COVID-19 Wotom Jeje kkar</a>. jerata, ko melele eok naloma jerbal jab mājet melele for ko konnaan kkar. </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>Eok kuwat lota bar juon kajjitōk for ko <a href='{webUrl}'>Kaajai COVID-19 Wotom Jeje</a>  kkar ippa a different joorkatkat teinwa nomba ak lota jipij, eok kuwat </a>kōkkeitaak koj</a>  bwe rejetak for jekkar ami jeje nana ami kokkeitaak melele, ak eok kuwat kokkeitaak ami naloma nan eok ami melele mmo kwo jjilōk nan ko konnaan kkar.</p>" +
                    $"<h2>Jeben kajjitōk?</h2>" +
                    $"<p>Ilomej arro <a href='{vaccineFAQUrl}'>Jokkutkut Kajjitok Nawawee (FAQ)</a> alal nan katak bar jidik ami laam jarom COVID-19 jaak jeje.</p>" +
                    $"<h2>Pad melele.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Mmat ko rimwik kojjela</a> on COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Wōpij Washington State Department of Health (Kutkutton konnaan jikuul in keenki) lota</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "mam" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Tz’ib’b’al te cheylakxta tej tx’u’j yab’il te COVID-19 toj xk’utz’ib’</h1>" +
                    $"<p>Ma’nxax nokx tqanay jun Tz’ib’b’al te cheylakxta tej tx’u’j yab’il te COVID-19 toj xk’utz’ib’ tej <a href='{webUrl}'>kloj te Tz’ib’b’al te cheylakxta tej tx’u’j yab’il te COVID-19 toj xk’utz’ib' </a>. Aj nojsamay, jq’umb’aj tumal xi q’o’ mi nel joniy tuya jq’umb’aj tumal toj jqeya qkloj te xk’utz’ib’. </ p >< br /> " +
                    $"<p>B’a’ ttzaj tsma’na junt qanb’al toj jkloj tej <a href='{webUrl}'>Tz’ib’b’al te cheylakxta tej tx’u’j yab’il te COVID-19 toj xk’utz’ib’</a>  tuya jun tajlal yolb’il niy eqat mo tb’i tb’e jun correo electrónico junxat, b’a’ <a href='{contactUsUrl}'>ttzaj q’ajtay q’i’ja</a>  te tu’ ttiq’ay onb’al te tu’ tel yoniy jtey ttz’ib’b’al tuya jtey tq’umb’aj tumal te tyolb’alay, mo b’a’ tyolana tuya jtey q’oltzta t-onb’al te tu’ntza tab’ij te qa tej tey tq’umb’aj tumal ot tz’ex sma’ toj kloj te kojb’il.</p>" +
                    $"<h2>¿At qaj tajay tzaj tqanay?</h2>" +
                    $"<p>Tokx toj jqeya qkloj che qaj Qanb’al jakax (<a href='{vaccineFAQUrl}'>Qe Xjel Kukx in che Tzaj Qanin (FAQ)</a>) te tu’ ttiq’ay kab’t q’umb’aj tumal tib’aj jtey Tqanil toj Yolb’il tun Tjyet COVID-19.</p>" +
                    $"<h2>Etkub’ tenay te ab’i’ chqil tumal tu’nay.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Ettiq’ay jq’umb’aj tumal ma’nxax nex q’umat</a>  tib’aj tx’u’j yab’il tej COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Correo electrónico te chqil Ja’ nik’ub’ aq’unt te Tb’anal xumlal tej Tnom te Washington [Washington State Department of Health, toj tyol me’x xjal] </p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "lo" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>ບັນທຶກການຢັ້ງຢືນ ພະຍາດ ໂຄວິດ-19 ແບບດີຈີຕອນ</h1>" +
                    $"<p>ເມື່ອບໍ່ດົນມານີ້ທ່ານໄດ້ຮ້ອງຂໍບັນທຶກການຢັ້ງຢືນ ພະຍາດ ໂຄວິດ-19 ແບບດີຈີຕອນ <a href='{webUrl}'>ລະບົບບັນທຶກການຢັ້ງຢືນ ພະຍາດ ໂຄວິດ-19 ແບບດີຈີຕອນ</a>. ໂຊກບໍ່ດີ, ຂໍ້ມູນທີ່ທ່ານສະໜອງໃຫ້ບໍ່ກົງກັບຂໍ້ມູນຢູ່ໃນລະບົບຂອງລັດ. </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>ທ່ານສາມາດສົ່ງຄໍາຮ້ອງຂໍອື່ນໃນລະບົບ <a href='{webUrl}'>ບັນທຶກການຢັ້ງຢືນ ພະຍາດ ໂຄວິດ-19 ແບບດີຈີຕອນ</a> ດ້ວຍເບີໂທລະສັບ ຫຼື ທີ່ຢູ່ອີເມວອື່ນ, ທ່ານສາມາດ <a href='{contactUsUrl}'>ຕິດຕໍ່ຫາພວກເຮົາ</a> ເພື່ອຂໍຄວາມຊ່ວຍເຫຼືອໃນການເຮັດບັນທຶກຂອງທ່ານ ກັບຂໍ້ມູນການຕິດຕໍ່ຂອງທ່ານກົງກັນ ຫຼື ທ່ານສາມາດຕິດຕໍ່ຫາຜູ້ໃຫ້ບໍລິການ ຂອງທ່ານເພື່ອໃຫ້ແນ່ໃຈວ່າຂໍ້ມູນຂອງທ່ານໄດ້ຖືກສົ່ງໃຫ້ລະບົບຂອງລັດແລ້ວ.</p>" +
                    $"<h2>ມີ​ຄຳ​ຖາມ​ບໍ?</h2>" +
                    $"<p>ເຂົ້າເບິ່ງໜ້າ<a href='{vaccineFAQUrl}'>ຄໍາ​ຖາມ​ທີ່​ຖືກ​ຖາມ​ເລື້ອຍໆ (FAQ)</a> ເພື່ອຮຽນ​ຮູ້​ເພີ່ມເຕີມກ່ຽວກັບບັນທຶກການຢັ້ງຢືນ ພະຍາດ ໂຄວິດ-19 ແບບດີຈີຕອນຂອງທ່ານ.</p>" +
                    $"<h2>ຕິດຕາມຂ່າວສານ.</h2>" +
                    $"<p><a href='{covidWebUrl}'>ເບິ່ງຂໍ້ມູນຫຼ້າສຸດ</a> ກ່ຽວກັບ ພະຍາດ ໂຄວິດ-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>ອີເມວທາງການຂອງ Washington State Department of Health (ພະແນກ ສຸຂະພາບ)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "km" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>កំណត់ត្រា​ផ្ទៀងផ្ទាត់​ជំងឹ​ COVID-19 ជាទម្រង់​ឌីជីថល​</h1>" +
                    $"<p>ថ្មីៗនេះ​ អ្នក​បានស្នើសុំកំណត់ត្រា​ផ្ទៀងផ្ទាត់​ជំងឹ​ COVID-19 ជាទម្រង់​ឌីជីថលពី <a href='{webUrl}'>ប្រព័ន្ធ​កំណត់ត្រា​ផ្ទៀងផ្ទាត់​ជំងឺ​ COVID-19 ជាទម្រង់​ឌីជីថល​។</a> គួរឲ្យសោកស្តាយ ព័ត៌មានដែលអ្នក​បានផ្តល់ជូននោះ​ មិនត្រូវគ្នា​ជាមួយ​​នឹង​ព័ត៌មានក្នុង​ប្រព័ន្ធរបស់រដ្ឋ​​យើង​ទេ​។ </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>អ្នកអាចប្រគល់សំណើផ្សេងទៀតនៅក្នុងប្រព័ន្ធ​ <a href='{webUrl}'>កំណត់ត្រាផ្ទៀងផ្ទាត់ជំងឺ COVID-19 ជាទម្រង់ឌីជីថល</a> ដែលមានលេខទូរសព្ទចល័ត និងអាសយដ្ឋានអ៊ីម៉ែលខុសគ្នា អ្នកអាច <a href='{contactUsUrl}'>ទាក់ទងមកយើង​</a> សម្រាប់ជំនួយក្នុងការផ្ទៀងផ្ទាត់កំណត់ត្រារបស់អ្នកជាមួយនឹងព័ត៌មានទំនាក់ទំនងរបស់អ្នក ឬអ្នកអាចទាក់ទងទៅកាន់អ្នកផ្តល់សេវារបស់អ្នកដើម្បីធានាថាព័ត៌មានត្រូវបានប្រគល់ទៅកាន់ប្រព័ន្ធរបស់រដ្ឋ។</p>" +
                    $"<h2>មានសំណួរមែនទេ?</h2>" +
                    $"<p>ចូលទៅកាន់ទំព័រ <a href='{vaccineFAQUrl}'>សំណួរចោទសួរជាញឹកញាប់ (FAQ)</a> របស់យើងដើម្បីស្វែងយល់បន្ថែមអំពីកំណត់ត្រាផ្ទៀងផ្ទាត់ជំងឺ COVID-19 ជាទម្រង់ឌីជីថលរបស់អ្នក។</p>" +
                    $"<h2>បន្តទទួលបានដំណឹង​​។</h2>" +
                    $"<p><a href='{covidWebUrl}'>ពិនិត្យមើលព័ត៌មានថ្មីៗ​បំផុត​</a> ស្តីពីជំងឺ​ COVID-19។</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>អ៊ីម៉ែលផ្លូវការរបស់​ Washington State Department of Health (ក្រសួងសុខាភិបាល​រដ្ឋ​វ៉ាស៊ីនតោន)។</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "kar" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>ဒံးကၠံၣ်တၢၣ်(လ) COVID-19 တၢ်အုၣ်သးတၢ်မၤနီၣ်မၤဃါ</h1>" +
                    $"<p>ဖဲတယံာ်ဒံးဘၣ်နဃ့ထီၣ် ဒံးကၠံၣ်တၢၣ်(လ) COVID-19 တၢ်အုၣ်သးတၢ်မၤနီၣ်မၤဃါ လၢ <a href='{webUrl}'>ဒံးကၠံၣ်တၢၣ်(လ) COVID-19 တၢ်အုၣ်သးတၢ်မၤနီၣ်မၤဃါတၢ်မၤအကျဲသနူ</a> လၢတၢ်တဘူၣ်ဂ့ၤတီၢ်ဘၣ်အပူၤ, တၢ်ဂ့ၢ်တၢ်ကျိၤလၢနဟ့ၣ်လီၤအံၤ တဘၣ်လိာ်ဒီးတၢ်ဂ့ၢ် တၢ်ကျိၤလၢကီၢ်စဲၣ်တၢ်မၤအကျဲသနူအပူၤဘၣ်. </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>နဆှၢထီၣ်တၢ်ဃ့ထီၣ်အဂၤတခါလၢ <a href='{webUrl}'>ဒံးကၠံၣ်တၢၣ်(လ) COVID-19 တၢ်အုၣ်သး တၢ်မၤနီၣ်မၤဃါ</a> တၢ်မၤအကျဲအပူၤဒီး လီတဲစိစိာ်စုနီၣ်ဂံၢ်လီၤဆီတဖျၢၣ် မ့တမ့ၢ် အံမ့(လ) န့ၣ်, န <a href='{contactUsUrl}'>ဆဲးကျၢပှၤလၢ</a> တၢ်မၤစၢၤအဂီၢ် လၢကဘၣ်လိာ်ဒီးနတၢ်မၤ နီၣ်မၤဃါ ဒီးနတၢ်ဆဲးကျၢတၢ်ဂ့ၢ်တၢ်ကျိၤ, မ့တမ့ၢ် နဆဲးကျၢနပှၤဟ့ၣ်တၢ်မၤစၢၤလၢက မၤလီၤတံၢ်နတၢ်ဂ့ၢ်တၢ်ကျိၤ လၢနဆှၢထီၣ်တ့ၢ်အီၤဆူကီၢ်စဲၣ်တၢ်မၤအကျဲသနူသ့န့ၣ်လီၤ.</p>" +
                    $"<h2>တၢ်သံကွၢ်အိၣ်ဧါ.</h2>" +
                    $"<p>လဲၤကွၢ်ဖဲ တၢ်သံကွၢ်လၢတၢ်သံကွၢ်အီၤခဲအံၤခဲအံၤ (<a href='{vaccineFAQUrl}'>တၢ်သံကွၢ်လၢဘၣ်တၢ်သံကွၢ်အီၤခဲအံၤခဲအံၤတဖၣ် (FAQ)</a>) ကဘျံးပၤလၢ ကမၤလိအါထီၣ်ဘၣ်ဃးဒီး န ဒံးကၠံၣ်တၢၣ်(လ) COVID-19 တၢ်အုၣ်သးတၢ်မၤနီၣ်မၤဃါ န့ၣ်တက့ၢ်.</p>" +
                    $"<h2>သ့ၣ်ညါတၢ်ဘိးဘၣ်သ့ၣ်ညါထီဘိ</h2>" +
                    $"<p><a href='{covidWebUrl}'>ကွၢ်တၢ်ဂ့ၢ်တၢ်ကျိၤလၢခံကတၢၢ်</a> ဘၣ်ဃးဒီး COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>၀ၣ် Washington State Department of Health (ရှ့ၣ်တၢၣ်ကီၢ်စဲၣ်တၢ်အိၣ်ဆူၣ်အိၣ်ချ့ဝဲၤကျိၤ)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "fj" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>iVolatukutuku Vakalivaliva ni iVakadinadina ni veika e Vauca na COVID-19</h1>" +
                    $"<p>O se qai kerea ga iVolatukutuku Vakalivaliva ni iVakadinadina ni veika e Vauca na COVID-19mai na <a href='{webUrl}'>misini ni iVolatukutuku Vakalivaliva ni iVakadinadina ni Veika e Vauca na COVID-19</a>. Na itukutuku oni vakarautaka e sega ni tautauvata kei na kena e maroroi tu ena matanitu.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>Oni rawa ni vakauta tale mai e dua kerekere ena <a href='{webUrl}'>iVolatukutuku ni iVakadinadina ni Veika e Vauca na COVID-19</a> ena dua tale na naba ni talevoni se imeli, o rawa ni <a href='{contactUsUrl}'>veitaratara kei keitou</a> me rawa ni keitou veivuke me salavata na itukutuku o vakarautaka kei na itukutuku ni veitaratara me baleti iko e tiko vei keitou, se o rawa ni veitaratara ina vanua o lai laurai kina mo taroga ke sa maroroi ina matanitu na kemu itukutuku.</p>" +
                    $"<h2>Taro?</h2>" +
                    $"<p>Rai ena tabana e tiko kina na <a href='{vaccineFAQUrl}'>Taro e Tarogi Wasoma (FAQ)</a> mo kila e levu tale na tikina e vauca na iVolatukutuku Vakalivaliva ni iVakadinadina ni veika e Vauca na COVID-19.</p>" +
                    $"<h2>Mo Kila na Veika e Yaco Tiko.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Kila na itukutuku vou duadua ena veika e vauca</a>  na COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>iMeli ni Washington State Department of Health (Tabana ni Bula ena Vanua o Washington)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "fa" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 dir='rtl' style='color: #C84C0E'>نسخه دیجیتال گواهی واکسیناسیون COVID-19</h1>" +
                    $"<p dir='rtl'>شما به‌تازگی «نسخه دیجیتال گواهی واکسیناسیون COVID-19» را از <a href='{webUrl}'>سیستم نسخه دیجیتال گواهی واکسیناسیون COVID-19</a>  درخواست کرده‌اید. متأسفانه، اطلاعاتی که ارائه کرده‌اید با اطلاعات موجود در سیستم ایالتی مطابقت ندارد. </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p dir='rtl'>می‌توانید با استفاده از یک شماره تلفن همراه یا نشانی ایمیل متفاوت، درخواست دیگری از‌طریق سیستم <a href='{webUrl}'>نسخه دیجیتال گواهی واکسیناسیون COVID-19</a> ارسال کنید، می‌توانید <a href='{contactUsUrl}'>با ما تماس بگیرید</a> تا برای مطابقت گواهی واکسیناسیون با اطلاعات تماستان به شما کمک کنیم، یا می‌توانید با ارائه‌دهنده خود تماس بگیرید تا مطمئن شوید که اطلاعات شما به سیستم ایالتی ارسال شده است.</p>" +
                    $"<h2 dir='rtl'>پرسشی دارید؟</h2>" +
                    $"<p dir='rtl'>برای کسب اطلاعات بیشتر در‌مورد «نسخه دیجیتال گواهی واکسیناسیون COVID-19»، به صفحه <a href='{vaccineFAQUrl}'>سؤالات متداول (FAQ)</a> ما مراجعه کنید.</p>" +
                    $"<h2 dir='rtl'>آگاه و مطلع بمانید.</h2>" +
                    $"<p dir='rtl'><a href='{covidWebUrl}'>جدیدترین اطلاعات</a>  مربوط به COVID-19 را مشاهده کنید.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p dir='rtl' style='text-align:center'>ایمیل رسمی Washington State Department of Health (اداره سلامت ایالت واشنگتن)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "prs" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 dir='rtl' style='color: #C84C0E'>سابقه دیجیتل تصدیق کووید-19</h1>" +
                    $"<p dir='rtl'>شما به تازگی فورم سابقه دیجیتل تصدیق کووید-19 را درخواست کردید <a href='{webUrl}'>سیستم سابقه دیجیتل تصدیق کووید-19</a> . متاسفانه، اطلاعاتی که شما ارائه نمودید با اطلاعات سیستم کشور مطابقت ندارد. </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p dir='rtl'>شما می توانید درخواست دیگری را در سیستم <a href='{webUrl}'>سابقه دیجیتل تصدیق کووید-19</a>  با یک نمبر تلفون یا ایمیل آدرسی متفاوت تسلیم نمایید، شما می توانید برای کمک به منظور مطابقت دادن سابقه خود با اطلاعات تماس خود <a href='{contactUsUrl}'>با ما تماس بگیرید</a>  ، یا شما می توانید به ارائه دهنده واکسین خود تماس گرفته تا مطمئن شوید اطلاعات شما به سیستم کشور تسلیم گردیده است.</p>" +
                    $"<h2 dir='rtl'>سوالاتی دارید؟</h2>" +
                    $"<p dir='rtl'><a href='{vaccineFAQUrl}'>از صفحه سوالات مکرراً پرسیده شده ما (FAQ)</a>  برای یادگیری بیشتر درباره سابقه دیجیتل واکسین کووید-19 بازدید کنید.</p>" +
                    $"<h2 dir='rtl'>مطلع بمانید.</h2>" +
                    $"<p dir='rtl'><a href='{covidWebUrl}'>مشاهده آخرین معلومات</a>  درباره کووید-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p dir='rtl' style='text-align:center'>ایمیل رسمی Washington State Department of Health (اداره صحت ایالت واشنگتن)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "chk" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Digital COVID-19 Afatan Record</h1>" +
                    $"<p>Ke Keran chok tungor ew Digital COVID-19 Afatan Record seni <a href='{webUrl}'>ewe Digital COVID-19 Afatan Record system</a>. Nge, ewe poraus ke awora ese mes ngeni met poraus mi nom non an state ei system ika nenien aisois. </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>En mi tongeni tinanong pwan ew tungor non ewe <a href='{webUrl}'>Digital COVID-19 Afatan Record</a> system ren om nounou pwan ew nampan fon ika email address, en mi tongeni <a href='{contactUsUrl}'>kokori ika churi kich</a> ren aninis ren ames fengeni met porausom me ifa usun ach sipwe kokoruk, ika en mi tongeni kokori om we selfon kompeni ren om kopwe enukunuku pwe met porausom a katonong non ewe state system.</p>" +
                    $"<h2>Mi wor om kapaseis?</h2>" +
                    $"<p>Feino katon ach kewe Kapas Eis Ekon Nap Ach Eis <a href='{vaccineFAQUrl}'>(Chechemeni kapas ais (FAQ))</a> pon ach we peich ren om kopwe awatenai om sinei usun noum Digital COVID-19 Afatan Record.</p>" +
                    $"<h2>Nonom nge Sisinei.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Katon minefon poraus</a> on COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>An Washington State Department of Health (Washington State Ofesin Pekin Safei) Email</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "my" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>ဒီဂျစ်တယ်လ် ကိုဗစ်-19 အတည်ပြုချက် မှတ်တမ်း</h1>" +
                    $"<p>မကြာသေးမီက သင်သည် <a href='{webUrl}'>ဒီဂျစ်တယ်လ် ကိုဗစ်-19 အတည်ပြုချက် မှတ်တမ်း စနစ်</a>  ထံမှ ဒီဂျစ်တယ်လ် ကိုဗစ်-19 အတည်ပြုချက် မှတ်တမ်း တစ်ခုကို တောင်းဆိုခဲ့ပါသည်။ ကံမကောင်းစွာဖြင့် သင်ပေးထားသော အချက်အလက်သည် ကျွန်ုပ်တို့ ပြည်နယ်စနစ်အတွင်းရှိ အချက်အလက် နှင့် ကိုက်ညီမှုမရှိပါ။ </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>သင်သည် အခြား ဖုန်းနံပါတ် သို့မဟုတ် အီးမေးလ်လိပ်စာ တစ်ခုဖြင့် <a href='{webUrl}'>ဒီဂျစ်တယ်လ် ကိုဗစ်-19 အတည်ပြုချက် မှတ်တမ်း</a> စနစ်ထဲတွင် တောင်းဆိုမှု နောက်တစ်ခုကို တင်ပြနိုင်သည်၊ သင့် ဆက်သွယ်ရန် အချက်အလက်အား သင့်မှတ်တမ်းနှင့်ကိုက်ညီစေရန် အကူအညီလိုလျှင် <a href='{contactUsUrl}'>ကျွန်ုပ်တို့ကို ဆက်သွယ်</a> နိုင်သည် သို့မဟုတ် သင့်အချက်အလက်အား ပြည်နယ်စနစ်ထဲသို့ တင်ပြထားခြင်းရှိမရှိကို သင့်စောင့်ရှောက်သူအား မေးမြန်းနိုင်ပါသည်။</p>" +
                    $"<h2>မေးခွန်းများရှိပါသလား။</h2>" +
                    $"<p>သင့် ဒီဂျစ်တယ်လ် ကိုဗစ်-19 အတည်ပြုချက် မှတ်တမ်းအကြောင်း ပိုမိုသိရှိလိုလျှင် ကျွန်ုပ်တို့၏ မေးလေ့မေးထရှိသောမေးခွန်းများ (<a href='{vaccineFAQUrl}'>မေးလေ့မေးထရှိသောမေးခွန်းများ</a>)ကို ဝင်ကြည့်ပါ။</p>" +
                    $"<h2>အချက်အလက်သိအောင်လုပ်ထားပါ</h2>" +
                    $"<p><a href='{covidWebUrl}'>နောက်ဆုံးအချက်အလက်များကို ကြည့်မည်</a> ကိုဗစ်-19 အကြောင်း</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Washington State Department of Health (တရားဝင် ဝါရှင်တန် ပြည်နယ် ကျန်းမာရေး ဌာန အီးမေးလ်)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "am" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>ዲጂታል የ COVID-19 ማረጋገጫ መዝገብ</h1>" +
                    $"<p>በቅርቡ ከ<a href='{webUrl}'>ዲጂታል የ COVID-19 ማረጋገጫ መዝገብ ስርዓት</a>  ዲጂታል የ COVID-19 ማረጋገጫ መዝገብ ጠይቀዋል። እንደ አለመታደል ሆኖ ያቀረቡት መረጃ በግዛቱ ሲስተም ውስጥ ካለው መረጃ ጋር አይዛመድም። </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>ሌላ ጥያቄ በ<a href='{webUrl}'>ዲጂታል COVID-19 ማረጋገጫ መዝገብ</a> በተለየ የሞባይል ስልክ ቁጥር ወይም ኢሜይል አድራሻ ማስገባት ይችላሉ፣ መዝገብዎን ከመገኛ መረጃዎ ጋር በማዛመድ የእኛን እርዳታ ያግኙ <a href='{contactUsUrl}'>ያግኙን</a> ፣ ወይም መረጃዎ ለግዛት ስርዓት መመዝገቡን ለማረጋገጥ አቅራቢዎን ማነጋገር ይችላሉ።</p>" +
                    $"<h2>ጥያቄዎች አሉዎት?</h2>" +
                    $"<p>ስለ ዲጂታል የ COVID-19 ክትባት መዝገብ የበለጠ ለማወቅ የእኛን <a href='{vaccineFAQUrl}'>ተዘውትረው የሚጠየቁ ጥያቄዎች</a> ገጽ ይጎብኙ።</p>" +
                    $"<h2>መረጃ ይኑርዎት።</h2>" +
                    $"<p>በ COVID-19 ላይ <a href='{covidWebUrl}'>የቅርብ ጊዜውን መረጃ ይመልከቱ</a> ።</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>ይፋዊ የ Washington State Department of Health (የዋሺንግተን ግዛት የጤና መምሪያ) ኢሜይል</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "om" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Mirkaneessa Ragaa Dijitaalaa COVID-19</h1>" +
                    $"<p>Dhiyeenyatti <a href='{webUrl}'>Mala Mirkaneessa Ragaa Dijitaalaa COVID-19</a> irraa Ragaa Mirkaneessa Dijitaalaa COVID-19 gaafattaniirtu. Akka carraa ta’ee, odeeffannoon isin laattan kan siistama keenya keessa jiruun wal hin simu. </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>Lakkoofsa bilbilaa yookin imeelii biraatin gaaffii biraa mala  <a href='{webUrl}'>Mirkaneessa Ragaa Dijitaalaa COVID-19</a> irratti galchuu dandeessu, ragaan keessan qunnamtii odeeffannoo keessan wajjin akka wal simuuf deeggarsa yoo feetan  <a href='{contactUsUrl}'>nu qunnamuu</a> dandeessu yookin odeeffannoon keessan siistama naannoo keessa galuu mirkaneeffachuuf dhiyeessaa keessan qunnamuu dandeessu.</p>" +
                    $"<h2>Gaaffii qabduu?</h2>" +
                    $"<p>Waa’ee Mirkaneessa Ragaa Dijitaalaa COVID-19 keessanii caalmatti baruuf, fuula <a href='{vaccineFAQUrl}'>Gaaffilee Yeroo Heddu Gaafataman (FAQ)</a> ilaalaa.</p>" +
                    $"<h2>Odeeffannoo Argadhaa.</h2>" +
                    $"<p>COVID-19 ilaalchisee  <a href='{covidWebUrl}'>odeeffannoo haaraa ilaalaa</a>.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Imeelii seera qabeessa kan Muummee Fayyaa Isteeta Washingtan (Washington State Department of Health)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "to" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Digital COVID-19 Verification Record (Lēkooti Fakamo’oni Huhu Malu'i COVID-19)</h1>" +
                    $"<p>Na’a ke toki kolé ni mai ‘a e foomu Lēkooti Fakamo’oni Huhu Malu’i COVID-19 meí he <a href='{webUrl}'>fa’unga tauhi Lēkooti Fakamo’oni Huhu Malu’i COVID-19</a>. Me’apango, ko e fakamatala ‘oku ke ‘omí ‘oku ‘ikai tatau ia mo e fakamatala ‘oku mau tauhí.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>Te ke lava ‘o fakahū ha’o toe kole ‘I he fa’unga <a href='{webUrl}'>Lēkooti Fakamo’oni Huhu Malu’i COVID-19</a> ’aki ha fika telefoni kehe pe tu’asila ‘imeili, te ke lava ‘o fetu’utaki mai  ki ha tokoni ki hono fakahoa ho’o lēkooti ki ho’o fakamatalá, pe ko ho’o fetu’utaki ho’o toketā ke fakapapau’i ‘oku fakahū atu ho’o fakamatalá.</p>" +
                    $"<h2>‘I ai ha ngaahi fehu’i?</h2>" +
                    $"<p>Vakai ki he’emau peesi Ngaahi Fehu’i ‘oku Fa’a ‘Eke Mai (<a href='{vaccineFAQUrl}'>Ngaahi Fehu’i ‘oku fa’a ‘Eke Mai</a>) ke toe ‘ilo lahiange fekau’aki mo ho’o Lēkooti Fakamo’oni Huhu Malu’i COVID-19.</p>" +
                    $"<h2>‘Ilo’i Maʻu Pē.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Vakai ki he fakamatala fakamuimui tahá</a> ’i he COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>'Imeili Faka'ofisiale Washington State Department Of Health (Potungāue Mo’ui ‘a e Siteiti ‘o Uasingatoní)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "ta" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>மின்னணு Covid-19 சரிபார்ப்புப் பதிவு</h1>" +
                    $"<p>சமீபத்தில் <a href='{webUrl}'>மின்னணு Covid-19 சரிபார்ப்புப் பதிவு அமைப்பிலிருந்த</a> மின்னணு Covid-19 சரிபார்ப்புப் பதிவைக் கோரியுள்ளீர்கள். துரதிர்ஷ்டவசமாக, நீங்கள் வழங்கிய தகவல் மாநில அமைப்பில் உள்ள தகவலுடன் பொருந்தவில்லை. </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>வேறு மொபைல் எண் அல்லது மின்னஞ்சல் முகவரியுடன் <a href='{webUrl}'>மின்னணு Covid-19 சரிபார்ப்புப் பதிவு</a>  அமைப்பில் மற்றொரு கோரிக்கையைச் சமர்ப்பிக்கலாம், உங்கள் தொடர்புத் தகவலுடன் உங்கள் பதிவைப் பொருத்துவதற்கான உதவிக்கு, நீங்கள் <a href='{contactUsUrl}'>எங்களைத் தொடர்புகொள்ளலாம்</a>  , அல்லது உங்கள் தகவல் மாநில அமைப்பில் சமர்ப்பிக்கப்பட்டுள்ளதா என்பதை உறுதிப்படுத்த உங்கள் வழங்குநரைத் தொடர்பு கொள்ளலாம்.</p>" +
                    $"<h2>கேள்விகள் உள்ளதா?</h2>" +
                    $"<p>உங்கள் மின்னணு கொவிட்-19 சரிபார்ப்புப் பதிவு பற்றி மேலும் அறிய, எங்களின் <a href='{vaccineFAQUrl}'>அடிக்கடி கேட்கப்படும் கேள்விகள் (FAQ)</a>  பக்கத்தைப் பார்வையிடவும்.</p>" +
                    $"<h2>தகவலை அறிந்து இருங்கள்.</h2>" +
                    $"<p><a href='{covidWebUrl}'>சமீபத்திய தகவலைப் பார்க்கவும்</a> கொவிட்-19 இல்.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>அதிகாரப்பூர்வ Washington State Department of Health (வாஷிங்டன் மாநில சுகாதாரத் துறை) மின்னஞ்சல்</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "hmn" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Kev Txheeb Xyuas Ntaub Ntawv Sau Tseg Txog Kab Mob COVID-19 Ua Dis Cis Tauj</h1>" +
                    $"<p>Tsis ntev los no koj tau thov Kev Txheeb Xyuas Ntaub Ntawv Sau Tseg Txog Kab Mob COVID-19 Ua Dis Cis Tauj los ntawm <a href='{webUrl}'>kev ua hauj lwm rau Kev Txheeb Xyuas Ntaub Ntawv Sau Tseg Txog Kab Mob COVID-19 Ua Dis Cis Tauj</a>. Hmoov tsis zoo, cov ntaub ntawv uas koj tau muab tsis raug raws li cov ntaub ntawv uas nyob rau hauv xeev txheej teg kev ua hauj lwm. </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>Koj tuaj yeem xa tau lwm qhov kev thov nyob rau hauv kev ua hauj lwm rau <a href='{webUrl}'>Kev Txheeb Xyuas Ntaub Ntawv Sau Tseg Txog Kab Mob COVID-19 Ua Dis Cis Tauj</a> nrog rau lwm tus nab npawb xov tooj ntawm tes los sis tus email, koj tuaj yeem <a href='{contactUsUrl}'>txuas lus tau rau peb</a> txhawm rau thov kev pab ua kom koj cov ntaub ntawv sau tseg raug raws li koj cov ntaub ntawv sib txuas lus, los sis koj tuaj yeem txuas lus tau rau koj tus kws pab kho mob txhawm rau ua kom ntseeg siab tias koj cov ntaub ntawv tau xa rau xeev txheej teg kev ua hauj lwm lawm.</p>" +
                    $"<h2>Puas muaj cov lus nug?</h2>" +
                    $"<p>Saib peb nplooj vev xaib muaj <a href='{vaccineFAQUrl}'>Cov Lus Nug Uas Nquag Nug (FAQ)</a> (Lus Askiv nkaus xwb) txhawm rau kawm paub ntxiv txog koj li Kev Txheeb Xyuas Ntaub Ntawv Sau Tseg Txog Kab Mob COVID-19 Ua Dis Cis Tauj.</p>" +
                    $"<h2>Soj Qab Saib Kev Paub.</h2>" +
                    $"<p><a href='{covidWebUrl}'>Saib cov ntaub ntawv tawm tshiab tshaj plaws</a> txog kab mob COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Tus Email Siv Raws Cai Ntawm Xeev Washington State Department of Health (Chav Hauj Lwm ntsig txog Kev Noj Qab Haus Huv)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                "th" => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>บันทึกการยืนยันเกี่ยวกับโควิด-19 แบบดิจิทัล </h1>" +
                    $"<p>คุณเพิ่งขอบันทึกการยืนยันเกี่ยวกับโควิด-19 แบบดิจิทัลจาก<a href='{webUrl}'>ระบบบันทึกการยืนยันเกี่ยวกับโควิด-19 แบบดิจิทัล</a> ขออภัย ข้อมูลที่คุณให้ไม่ตรงกับข้อมูลในระบบของรัฐ </p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<p>คุณสามารถส่งคำขออื่นได้ในระบบ<a href='{webUrl}'>บันทึกการยืนยันเกี่ยวกับโควิด-19 แบบดิจิทัล</a> โดยใช้หมายเลขโทรศัพท์มือถือหรือที่อยู่อีเมลอื่น คุณสามารถ<a href='{contactUsUrl}'>ติดต่อเรา</a> เพื่อขอความช่วยเหลือในการจับคู่บันทึกของคุณกับข้อมูลติดต่อของคุณ หรือคุณสามารถติดต่อผู้ให้บริการของคุณเพื่อให้แน่ใจว่ามีการส่งข้อมูลของคุณไปยังระบบของรัฐแล้ว</p>" +
                    $"<h2>มีคำถามหรือไม่</h2>" +
                    $"<p>โปรดไปยังส่วน<a href='{vaccineFAQUrl}'>คำถามที่พบบ่อย (FAQ)</a> เพื่อเรียนรู้เพิ่มเติมเกี่ยวกับบันทึกการยืนยันเกี่ยวกับโควิด-19 แบบดิจิทัลของคุณ</p>" +
                    $"<h2>คอยติดตามข่าวสาร</h2>" +
                    $"<p><a href='{covidWebUrl}'>ดูข้อมูลล่าสุด</a> เกี่ยวกับโควิด-19</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>อีเมลอย่างเป็นทางการของ Washington State Department of Health (กรมอนามัยของรัฐวอชิงตัน)</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>",
                _ => $"<img src='{webUrl}/imgs/waverifylogo.png' alt='WA Verify Logo'><p style='margin: 0; padding: 0; line-height: 0;' />" +
                    $"<h1 style='color: #C84C0E'>Digital COVID-19 Verification Record</h1>" +
                    $"<p>You recently requested a Digital COVID-19 Verification Record from the <a href='{webUrl}'>Digital COVID-19 Verification Record system</a>. Unfortunately, the information you provided does not match information in the state system. " +
                    $"<p>You can submit another request in the <a href='{webUrl}'>Digital COVID-19 Verification Record system</a> with a different mobile phone number or email address, you can <a href='{contactUsUrl}'>contact us</a> for help in matching your record to your contact information, or you can contact your provider to ensure your information has been submitted to the state system.</p>" +
                    $"<h2>Have questions?</h2>" +
                    $"<p>Visit our <a href='{vaccineFAQUrl}'>Frequently Asked Questions (FAQ)</a> page to learn more about your Digital COVID-19 Verification Record.</p>" +
                    $"<h2>Stay Informed.</h2>" +
                    $"<p><a href='{covidWebUrl}'>View the latest information</a> on COVID-19.</p><p style='margin: 0; padding: 0; line-height: 2.4;' />" +
                    $"<hr>" +
                    $"<footer><p style='text-align:center'>Official Washington State Department of Health e-mail</p>" +
                    $"<p style='text-align:center'><img src='{emailLogoUrl}' alt='Washington State Department of Health Logo'></p></footer>"
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