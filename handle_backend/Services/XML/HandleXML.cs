using handle_backend.Services.Firebase;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace handle_backend.Services.XML
{
  
    public class HandleXML
    {
        private readonly FirebaseService _firebase;

        public HandleXML(FirebaseService firebase)
        {
            _firebase = firebase;
        }
        public static bool IsBase64String(string s)
        {
            s = s.Trim();
            if (s.Length % 4 != 0) return false;
            try
            {
                Convert.FromBase64String(s);
                return true;
            }
            catch
            {
                return false;
            }
        }


        public void AnalysXML130(string fileName)
        {
            //Console.WriteLine($"🔍 Bắt đầu check file: {fileName}");

            try
            {
                if (!File.Exists(fileName))
                {
                    Console.WriteLine($"⚠️ File không tồn tại: {fileName}");
                    return;
                }

                XDocument doc = XDocument.Load(fileName);
                var fileHosos = doc.Descendants("FILEHOSO").ToList();
                var nodeYL1 = doc.Descendants("HOSO").FirstOrDefault();

                if (!fileHosos.Any())
                {
                    Console.WriteLine($"⚠️ Không tìm thấy thẻ <FILEHOSO> trong {fileName}");
                    return;
                }

                if (nodeYL1 == null)
                {
                    Console.WriteLine($"⚠️ Không tìm thấy thẻ <HOSO> trong {fileName}");
                    return;
                }

                var first = fileHosos.First();
                var firstValue = first.Elements().Skip(1).FirstOrDefault()?.Value?.Trim() ?? "";

                if (string.IsNullOrEmpty(firstValue))
                {
                    Console.WriteLine($"⚠️ FILEHOSO đầu tiên trong {fileName} không có nội dung");
                    return;
                }

                if (IsBase64String(firstValue))
                {
                    foreach (var f in fileHosos)
                    {
                        var loai = f.Elements().FirstOrDefault()?.Value?.Trim() ?? "(Không có loại)";
                        var base64Content = f.Elements().Skip(1).FirstOrDefault()?.Value?.Trim();

                        if (string.IsNullOrEmpty(base64Content))
                            continue;

                        try
                        {
                            byte[] data = Convert.FromBase64String(base64Content);
                            string decodedString = Encoding.UTF8.GetString(data);
                            var (patientName, patientId, errors) = SaveAndCheck(loai, decodedString);
                            LogErrors(patientName, patientId, errors, loai, fileName);
                        }
                        catch (FormatException)
                        {
                            Console.WriteLine($"⚠️ Lỗi Base64 không hợp lệ trong file: {fileName}");
                        }
                    }
                }
                else
                {
                    var decodedString2 = nodeYL1.ToString(SaveOptions.DisableFormatting);
                    var fileHosoList = nodeYL1.Elements("FILEHOSO").ToList();

                    if (!fileHosoList.Any())
                    {
                        Console.WriteLine($"⚠️ Không tìm thấy FILEHOSO trong {fileName}");
                        return;
                    }

                    foreach (var fileHoso in fileHosoList)
                    {
                        var loai = fileHoso.Element("LOAIHOSO")?.Value?.Trim() ?? "(Không có loại hồ sơ)";
                        var (patientName, patientId, errors) = SaveAndCheck(loai, decodedString2);
                        LogErrors(patientName, patientId, errors, loai, fileName);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ Lỗi xử lý file {fileName}: {e.Message}");
            }
            finally
            {
                Console.WriteLine($"✅ Đã check xong file {fileName}");
            }
        }
        #region Log Errors
        private void LogErrors(string patientName, string patientId, List<string> errors, string xmlType, string fileName)
        {
            if (string.IsNullOrEmpty(patientName) || string.IsNullOrEmpty(patientId))
            {
                //Console.WriteLine($"⚠️ Không xác định được thông tin bệnh nhân trong {xmlType} của file {fileName}");
                return;
            }

            if (errors.Any())
            {
                var grouped = errors
                .GroupBy(e => e)
                .Select(g => $"{g.Key} ({g.Count()} " +
                $"" +
                $"" +
                $"lần)")
                .ToList();

                Console.WriteLine($"Hồ sơ bệnh nhân {patientName} mã bệnh nhân {patientId} có {errors.Count} lỗi trong {xmlType}: ");
                foreach (var err in grouped)
                {
                    Console.WriteLine($" - {err}");
                }


                //string errorList = string.Join("", errors);
                string errorText = string.Join("; ", grouped);
                _ = _firebase.AddError_BHYT(patientName, patientId, $"[{xmlType}] {errorText}");

            }
            else
            {
                //Console.WriteLine($"Hồ sơ bệnh nhân {patientName} mã bệnh nhân {patientId} trong {xmlType} không có lỗi");
            }
        }
        #endregion
        // Xử lý dữ liệu đầu vào trước khi xử lý
        static string CleanInput(string input)
        {
            input = input.Trim();

            // 1. Đổi () thành [] và xóa khoảng trắng dư
            input = input.Replace('(', '[').Replace(')', ']');
            input = Regex.Replace(input, @"\s*\[\s*", "[");  // Bỏ space trước [
            input = Regex.Replace(input, @"\s*\]\s*", "]");  // Bỏ space trước ]
                                                             // Thêm dòng xử lý hậu kỳ
            var lastIndex = input.LastIndexOf(']');
            if (lastIndex >= 0 && lastIndex + 1 < input.Length)
            {
                input = input.Substring(0, lastIndex + 1);
            }                                             // Thêm dấu ] nếu thiếu
            if (!input.Contains(']') && input.Contains('['))
            {
                input += "]";
            }
            // Thêm [ hoặc ] nếu thiếu
            if (!input.Contains('['))
            {
                input = Regex.Replace(input, @"(ngày)(\d)", "$1[$2");
            }
            // Thay dấu phẩy ',' thành dấu chấm '.'
            input = input.Replace(',', '.');

            // 2. Chuẩn hóa số và đơn vị: 0 .2 lọ -> 0.2 lọ, 15mci -> 15 mci
            input = Regex.Replace(input, @"(^|\s)(\d*\.?\d+)([a-zA-Z])", "$1$2 $3");
            input = Regex.Replace(input, @"(\d)\s*\.\s*(\d)", "$1.$2");        // 0 .2 → 0.2
            input = Regex.Replace(input, @"(\d)([a-zA-Z])", "$1 $2");          // 15mci → 15 mci
            input = Regex.Replace(input, @"(\d*\.\d+)([a-zA-Z])", "$1 $2");    // 0.2lọ → 0.2 lọ
            input = Regex.Replace(input, @"(\d*\.?\d+)\s*\.\s*([a-zA-Z])", "$1 $2"); //0.2.lọ → 0.2 lọ
            input = Regex.Replace(input, @"(\d*\.?\d+)\s+([^\d\s\*/\[\]]+)", "$1 $2"); // giữ khoảng trắng hợp lệ
            input = Regex.Replace(input, @"(\d*\.?\d+)\s+([a-zA-Z]+)", "$1 $2"); // đảm bảo cách giữa số và đơn vị

            // 3. Xóa khoảng trắng dư quanh ký hiệu
            input = Regex.Replace(input, @"\s*/\s*", "/");
            input = Regex.Replace(input, @"\s*\*\s*", "*");
            input = Regex.Replace(input, @"\s*\[\s*", "[");
            input = Regex.Replace(input, @"\s*\]\s*", "]");

            // --- Thêm dòng này xử lý / đơn vị bị cách ra ---
            input = Regex.Replace(input, @"/\s+([^\d\*/\[\]\s]+)", "/$1");

            // 4. Xử lý / dư thừa
            input = Regex.Replace(input, @"/{2,}", "/");
            input = Regex.Replace(input, @"(?<![a-zA-Z])/(\d)", "*$1");
            input = Regex.Replace(input, @"(?<![a-zA-Z])/(\d*\.?\d+)", "*$1");
            input = Regex.Replace(input, @"(/[^\d/\*\[\]\s]+)/(?![^\d])", "$1*");

            // 5. Gộp khoảng trắng thừa
            input = Regex.Replace(input, @"\s{2,}", " ");

            if (Regex.IsMatch(input, @"^(\d*\.?\d+)/lần", RegexOptions.IgnoreCase))
            {
                input = Regex.Replace(input, @"^(\d*\.?\d+)/lần", "$1 lọ/lần", RegexOptions.IgnoreCase);
            }

            // 6. Thêm đơn vị còn thiếu trong [ ] nếu cần
            var matchKetThuc = Regex.Match(input, @"\[(\d*\.?\d+)\s*/ngày\]", RegexOptions.IgnoreCase);
            var matchDonVi = Regex.Match(input, @"^(\d*\.?\d+)\s*([^\d/\*\[\]]+)?/lần", RegexOptions.IgnoreCase);
            if (matchDonVi.Success && matchKetThuc.Success)
            {
                var dv = string.IsNullOrWhiteSpace(matchDonVi.Groups[2].Value) ? "lọ" : matchDonVi.Groups[2].Value;
                var sl = matchKetThuc.Groups[1].Value;
                input = Regex.Replace(input, @"\[\d*\.?\d+\s*/ngày\]", $"[{sl} {dv}/ngày]");
            }

            return input.Trim();
        }
        public bool SosanhTime(string Datetime1, string Datetime2, int sophut)
        {
            if (string.IsNullOrWhiteSpace(Datetime1) || string.IsNullOrWhiteSpace(Datetime2))
                return false;

            const string format = "yyyyMMddHHmm";
            if (!DateTime.TryParseExact(Datetime1, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time1))
                return false;
            if (!DateTime.TryParseExact(Datetime2, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time2))
                return false;

            TimeSpan chenhLech = (time1 - time2).Duration();
            return chenhLech <= TimeSpan.FromMinutes(sophut); // dùng <= nếu muốn cho phép bằng
        }

        public bool checkChar(string input)
        {
            foreach (char c in input)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == UnicodeCategory.NonSpacingMark ||
                    category == UnicodeCategory.SpacingCombiningMark ||
                    category == UnicodeCategory.EnclosingMark)
                {
                    return true;
                }
            }
            return false;
        }

        public bool SosanhTime2(string Datetime1, string Datetime2)
        {
            if (string.IsNullOrWhiteSpace(Datetime1) || string.IsNullOrWhiteSpace(Datetime2))
                return false;

            const string format = "yyyyMMddHHmm";

            if (!DateTime.TryParseExact(Datetime1, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time1))
                return false;

            if (!DateTime.TryParseExact(Datetime2, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time2))
                return false;

            return time1 <= time2;
        }


        public bool checkformat(string checkformat, string regex)
        {
            Regex regex1 = new Regex(regex);
            bool ketqua;
            if (!regex1.IsMatch(checkformat))
            {
                ketqua = false;
            }
            else
            {
                ketqua = true;
            }
            return ketqua;
        }

        private (string PatientName, string PatientId, List<string> Errors) SaveAndCheck(string strNode, string decodedString)
        {
            List<string> errors = new List<string>();
            string maBN_string = "";
            string tenBN_string = "";

            string mattdv_thayHung = "3096009023";
            string mattdv_thayTrung = "2997032342";
            int soluongValue = 0;
            string ngay_vv = "";
            string ngay_rv = "";
            string ngaythanhtoan = "";

            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(decodedString);
                XmlNode malk = xmlDoc.SelectSingleNode("//MA_LK");

                // Extract patient information
                XmlNodeList nodeYL2 = xmlDoc.SelectNodes("//TONG_HOP");
                if (nodeYL2 != null && nodeYL2.Count > 0)
                {
                    foreach (XmlNode chiTietNode in nodeYL2)
                    {
                        XmlNode mabn = chiTietNode.SelectSingleNode("MA_BN");
                        XmlNode hotenbn = chiTietNode.SelectSingleNode("HO_TEN");
                        maBN_string = mabn?.InnerText.Trim() ?? "";
                        tenBN_string = hotenbn?.InnerText.Trim() ?? "";
                    }
                }

                if (strNode == "XML1")
                {
                    XmlNodeList nodeYL = xmlDoc.SelectNodes("//TONG_HOP");
                    if (nodeYL != null && nodeYL.Count > 0)
                    {
                        foreach (XmlNode chiTietNode in nodeYL)
                        {
                            XmlNode mabn = chiTietNode.SelectSingleNode("MA_BN");
                            XmlNode hotenbn = chiTietNode.SelectSingleNode("HO_TEN");
                            XmlNode socccd = chiTietNode.SelectSingleNode("SO_CCCD");
                            XmlNode ngaysinh = chiTietNode.SelectSingleNode("NGAY_SINH");
                            XmlNode gioitinh = chiTietNode.SelectSingleNode("GIOI_TINH");
                            XmlNode diachi = chiTietNode.SelectSingleNode("DIA_CHI");
                            XmlNode mathebhyt = chiTietNode.SelectSingleNode("MA_THE_BHYT");
                            XmlNode ngayvaonoitru = chiTietNode.SelectSingleNode("NGAY_VAO_NOI_TRU");
                            XmlNode ngayttoan = chiTietNode.SelectSingleNode("NGAY_TTOAN");
                            XmlNode ma_ttdv = chiTietNode.SelectSingleNode("MA_TTDV");
                            XmlNode namqt = chiTietNode.SelectSingleNode("NAM_QT");
                            XmlNode thangqt = chiTietNode.SelectSingleNode("THANG_QT");
                            XmlNode maloaikcb = chiTietNode.SelectSingleNode("MA_LOAI_KCB");
                            XmlNode makhoa = chiTietNode.SelectSingleNode("MA_KHOA");
                            XmlNode macskcb = chiTietNode.SelectSingleNode("MA_CSKCB");
                            XmlNode giatritu = chiTietNode.SelectSingleNode("GT_THE_TU");
                            XmlNode giatriden = chiTietNode.SelectSingleNode("GT_THE_DEN");
                            XmlNode ngayvao = chiTietNode.SelectSingleNode("NGAY_VAO");
                            XmlNode ngayra = chiTietNode.SelectSingleNode("NGAY_RA");
                            XmlNode lydovnt = chiTietNode.SelectSingleNode("LY_DO_VNT");
                            XmlNode lydovv = chiTietNode.SelectSingleNode("LY_DO_VV");
                            XmlNode mabenhchinh = chiTietNode.SelectSingleNode("MA_BENH_CHINH");
                            XmlNode chandoanrv = chiTietNode.SelectSingleNode("CHAN_DOAN_RV");
                            XmlNode chandoanvv = chiTietNode.SelectSingleNode("CHAN_DOAN_VAO");
                            XmlNode maquoctich = chiTietNode.SelectSingleNode("MA_QUOCTICH");
                            XmlNode ppdieutri = chiTietNode.SelectSingleNode("PP_DIEU_TRI");
                            XmlNode MA_NGHE_NGHIEP = chiTietNode.SelectSingleNode("MA_NGHE_NGHIEP");

                            ngay_rv = ngayra?.InnerText.Trim() ?? "";
                            ngay_vv = ngayvao?.InnerText.Trim() ?? "";
                            ngaythanhtoan = ngayttoan?.InnerText.Trim() ?? "";

                            if (lydovv != null && string.IsNullOrWhiteSpace(lydovv.InnerText))
                                errors.Add("Trường LY_DO_VV không được để trống" + "\n");
                            /*if (ppdieutri != null && string.IsNullOrWhiteSpace(ppdieutri.InnerText))
                                errors.Add("Trường PP_DIEU_TRI không được để trống" + "\n");*/
                            if (mabenhchinh != null && string.IsNullOrWhiteSpace(mabenhchinh.InnerText))
                                errors.Add("Trường mã bệnh chính MA_BENH_CHINH không được để trống" + "\n");
                            if (chandoanvv != null && string.IsNullOrWhiteSpace(chandoanvv.InnerText))
                                errors.Add("Trường chẩn đoán vào viện CHAN_DOAN_VAO không được để trống" + "\n");
                            if (chandoanrv != null && string.IsNullOrWhiteSpace(chandoanrv.InnerText))
                                errors.Add("Trường chẩn đoán ra viện CHAN_DOAN_RV không được để trống" + "\n");
                            if (MA_NGHE_NGHIEP != null && string.IsNullOrWhiteSpace(MA_NGHE_NGHIEP.InnerText))
                                errors.Add("MA_NGHE_NGHIEP không được để trống" + "\n");
                            if (maquoctich != null && maquoctich.InnerText != "000")
                                errors.Add("Sai quốc tịch" + "\n");
                            if (lydovnt != null && string.IsNullOrWhiteSpace(lydovnt.InnerText) && maloaikcb?.InnerText == "3")
                                errors.Add("Trường LY_DO_VNT không được để trống" + "\n");
                            if (ngayra != null && ngayttoan != null && !SosanhTime2(ngayra.InnerText, ngayttoan.InnerText))
                                errors.Add("Ngày thanh toán không được nhỏ hơn ngày ra viện"  );
                            if (ngayra != null && ngayvao != null && !SosanhTime2(ngayvao.InnerText, ngayra.InnerText))
                                errors.Add("Ngày ra không được nhỏ hơn ngày vào viện"   );
                            if (ngayttoan != null && ngayvao != null && !SosanhTime2(ngayvao.InnerText, ngayttoan.InnerText))
                                errors.Add("Ngày thanh toán không được nhỏ hơn ngày vào viện" + "\n");
                            if (giatritu != null && giatriden != null && !SosanhTime2(giatritu.InnerText + "0000", giatriden.InnerText + "0000"))
                                errors.Add($"Trường hạn thẻ không đúng hoặc GT_THE_DEN {giatriden.InnerText} nhỏ hơn GT_THE_TU {giatritu.InnerText}" + "\n");
                            if (namqt != null && string.IsNullOrWhiteSpace(namqt.InnerText))
                                errors.Add("Năm quyết toán không được để trống" + "\n");
                            if (thangqt != null && string.IsNullOrWhiteSpace(thangqt.InnerText))
                                errors.Add("Tháng quyết toán không được để trống" + "\n");
                            if (socccd != null && !string.IsNullOrEmpty(socccd.InnerText) && !checkformat(socccd.InnerText, @"^\d{12}$"))
                                errors.Add("Thẻ căn cước không đúng định dạng" + "\n");
                            if (ma_ttdv != null && (ma_ttdv.InnerText != mattdv_thayHung && ma_ttdv.InnerText != mattdv_thayTrung))
                                errors.Add("Thông tin MA_TTDV sai" + "\n");
                        }
                    }
                }

                if (strNode == "XML2")
                {
                    XmlNodeList nodeYL = xmlDoc.SelectNodes("//CHI_TIET_THUOC");
                    if (nodeYL != null && nodeYL.Count > 0)
                    {
                        foreach (XmlNode chiTietNode in nodeYL)
                        {
                            XmlNode mathuoc = chiTietNode.SelectSingleNode("MA_THUOC");
                            XmlNode lieudung = chiTietNode.SelectSingleNode("LIEU_DUNG");
                            XmlNode ttthau = chiTietNode.SelectSingleNode("TT_THAU");
                            XmlNode soluong = chiTietNode.SelectSingleNode("SO_LUONG");
                            XmlNode MA_BAC_SI = chiTietNode.SelectSingleNode("MA_BAC_SI");
                            XmlNode NGAY_YL = chiTietNode.SelectSingleNode("NGAY_YL");
                            XmlNode NGAY_TH_YL = chiTietNode.SelectSingleNode("NGAY_TH_YL");
                            XmlNode SO_DANG_KY = chiTietNode.SelectSingleNode("SO_DANG_KY");
                            XmlNode TEN_THUOC = chiTietNode.SelectSingleNode("TEN_THUOC");

                            if (mathuoc != null && !checkformat(mathuoc.InnerText.Trim(), @"^(\d+(\.\d+){1,2}|\d+[A-Za-z]\.\d+)$"))
                                errors.Add($"Mã thuốc {mathuoc.InnerText} sai định dạng mã thuốc MA_THUOC theo quy định" + "\n");
                            if (mathuoc != null)
                            {
                                string input = CleanInput(lieudung?.InnerText.Trim() ?? "");
                                string inputOriginal = lieudung?.InnerText.Trim() ?? "";
                                bool isFormatCorrect = checkformat(input, @"^(\d+(/\d+)?|\d*\.?\d+)\s*\w+(?:\s\w+)*\s*/\s*lần\s*\*\s*(\d+(/\d+)?|\d*\.?\d+)\s*lần/ngày\s*\*\s*(\d+(/\d+)?|\d*\.?\d+)\s*ngày\s*\[\s*(\d+(/\d+)?|\d*\.?\d+)\s*\w+(?:\s\w+)*\s*/\s*ngày\s*\]$");
                                bool isValid = Regex.IsMatch(inputOriginal, @"^(?:(Sáng|Trưa|Chiều|Tối):\s*\d*\.?\d+\s*\w+\s*,\s*)*(Sáng|Trưa|Chiều|Tối):\s*\d*\.?\d+\s*\w+\s*(\*\s*\d+\s*ngày)?\s*\[\s*\d*\.?\d+\s*\w+\s*/ngày\s*\]$", RegexOptions.IgnoreCase);
                                if (!isFormatCorrect && !isValid)
                                    errors.Add($"Mã thuốc {mathuoc.InnerText} Sai định dạng LIEU_DUNG theo quy định" + "\n");
                            }
                            if (ttthau != null && !string.IsNullOrEmpty(ttthau.InnerText) && mathuoc?.InnerText != "40.17" && !checkformat(ttthau.InnerText.Trim(), @"^[\p{L}0-9/_-]+;[A-Z0-9]+;[A-Z0-9]+;\d{4}$"))
                                errors.Add(string.IsNullOrEmpty(ttthau.InnerText) ? "Thông tin thầu TT_THAU không được để trống" : $"Sai thông tin thầu {ttthau} theo quy định" + "\n");
                            if (soluong != null && int.TryParse(soluong.InnerText.Trim(), out soluongValue) && soluongValue <= 0)
                                errors.Add("Số lượng không được nhỏ hơn 0" + "\n");
                            if (MA_BAC_SI != null && string.IsNullOrWhiteSpace(MA_BAC_SI.InnerText.Trim()))
                                errors.Add("Mã chứng chỉ hành nghề của bác sỹ không được để trống" + "\n");
                            if (MA_BAC_SI != null && !checkformat(MA_BAC_SI.InnerText.Trim(), @"^\d{6}/[A-Z]{2,3}-[A-Z]{4}$"))
                                errors.Add("Mã chứng chỉ hành nghề của bác sỹ không đúng định dạng" + "\n");
                            if (NGAY_YL != null && NGAY_TH_YL != null && !string.IsNullOrWhiteSpace(NGAY_YL.InnerText) && !string.IsNullOrWhiteSpace(NGAY_TH_YL.InnerText) && !SosanhTime2(NGAY_YL.InnerText, NGAY_TH_YL.InnerText))
                                errors.Add("NGAY_TH_YL không được nhỏ hơn NGAY_YL" + "\n");
                            if (NGAY_YL != null && !string.IsNullOrWhiteSpace(NGAY_YL.InnerText) && !string.IsNullOrWhiteSpace(ngay_rv) && !SosanhTime2(NGAY_YL.InnerText, ngay_rv))
                                errors.Add("NGAY_RA không được nhỏ hơn NGAY_YL" + "\n");
                            if (NGAY_YL != null && !string.IsNullOrWhiteSpace(NGAY_YL.InnerText) && !string.IsNullOrWhiteSpace(ngay_vv) && !SosanhTime2(ngay_vv, NGAY_YL.InnerText))
                                errors.Add("NGAY_YL không được nhỏ hơn NGAY_VAO" + "\n");
                            if (NGAY_TH_YL != null && !string.IsNullOrWhiteSpace(NGAY_TH_YL.InnerText) && !string.IsNullOrWhiteSpace(ngay_rv) && !SosanhTime2(NGAY_TH_YL.InnerText, ngay_rv))
                                errors.Add("NGAY_RA không được nhỏ hơn NGAY_TH_YL" + "\n");
                            if (NGAY_TH_YL != null && !string.IsNullOrWhiteSpace(NGAY_TH_YL.InnerText) && !string.IsNullOrWhiteSpace(ngay_vv) && !SosanhTime2(ngay_vv, NGAY_TH_YL.InnerText))
                                errors.Add("NGAY_TH_YL không được nhỏ hơn NGAY_VAO" + "\n");
                            if (SO_DANG_KY != null && string.IsNullOrWhiteSpace(SO_DANG_KY.InnerText) && mathuoc?.InnerText != "40.17")
                                errors.Add("SO_DANG_KY không được để trống" + "\n");
                            if (TEN_THUOC != null && checkChar(TEN_THUOC.InnerText) && !string.IsNullOrWhiteSpace(mathuoc?.InnerText))
                                errors.Add($"TEN_THUOC {TEN_THUOC.InnerText} có định dạng không phải là Unicode tổ hợp" + "\n");
                        }
                    }
                }
                
                
                if (strNode == "XML3")
                {
                    XmlNodeList nodeYL = xmlDoc.SelectNodes("//CHI_TIET_DVKT");
                    if (nodeYL != null && nodeYL.Count > 0)
                    {
                        foreach (XmlNode chiTietNode in nodeYL)
                        {
                            XmlNode MA_DICH_VU = chiTietNode.SelectSingleNode("MA_DICH_VU");
                            XmlNode MA_VAT_TU = chiTietNode.SelectSingleNode("MA_VAT_TU");
                            XmlNode ttthau = chiTietNode.SelectSingleNode("TT_THAU");
                            XmlNode soluong = chiTietNode.SelectSingleNode("SO_LUONG");
                            XmlNode MA_BAC_SI1 = chiTietNode.SelectSingleNode("MA_BAC_SI");
                            XmlNode NGUOI_THUC_HIEN = chiTietNode.SelectSingleNode("NGUOI_THUC_HIEN");
                            XmlNode NGAY_YL = chiTietNode.SelectSingleNode("NGAY_YL");
                            XmlNode NGAY_TH_YL = chiTietNode.SelectSingleNode("NGAY_TH_YL");
                            XmlNode NGAY_KQ = chiTietNode.SelectSingleNode("NGAY_KQ");
                            XmlNode TEN_DICH_VU = chiTietNode.SelectSingleNode("TEN_DICH_VU");
                            XmlNode TEN_VAT_TU = chiTietNode.SelectSingleNode("TEN_VAT_TU");
                            XmlNode MA_NHOM = chiTietNode.SelectSingleNode("MA_NHOM");
                            XmlNode MA_MAY = chiTietNode.SelectSingleNode("MA_MAY");
                            if (MA_DICH_VU.InnerText != "")
                            {
                                if (MA_DICH_VU != null && checkformat(MA_DICH_VU.InnerText.Trim(), @"^[A-Za-z]*\d+(\.\d+)+$") == false)
                                {
                                    errors.Add($"Sai định dạng mã thuốc dịch vụ {MA_DICH_VU.InnerText} theo quy định" + "\n");
                                }
                            }
                            if (MA_VAT_TU != null && MA_VAT_TU.InnerText != "")
                            {
                                if (checkformat(MA_VAT_TU.InnerText.Trim(), @"^[A-Z]\d{2}\.\d{2}\.\d{3}\.\d{4}\.\d{3}\.\d{4}$") == false)
                                {
                                    errors.Add($"Sai định dạng mã vật tư {MA_DICH_VU.InnerText} theo quy định" + "\n");
                                }
                            }

                            if (ttthau != null && MA_VAT_TU != null && checkformat(ttthau.InnerText.Trim(), @"^[\p{L}0-9/-]+;[\p{L}0-9]+;[\p{L}0-9]+;\d{4}$") == false &&
                                ttthau != null &&
                           !string.IsNullOrWhiteSpace(ttthau.InnerText) && MA_VAT_TU.InnerText != "")
                            {
                                if (ttthau.InnerText == "")
                                {
                                    errors.Add( "Thông tin thầu TT_THAU không được để trống." + "\n");
                                }
                                else
                                {
                                    errors.Add($"Sai thông tin thầu TT_THAU {ttthau.InnerText} theo quy định." + "\n");
                                }

                            }
                            if (MA_MAY != null && checkformat(MA_MAY.InnerText.Trim(), @"^[A-ZĐ]{2,4}\d?\.\d+\.\d+\.[A-Z0-9]+$") == false &&
                                MA_MAY != null && !string.IsNullOrWhiteSpace(MA_MAY.InnerText.Trim()) && MA_DICH_VU.InnerText.Trim() != "")
                            {
                                errors.Add($"Sai thông tin {MA_MAY.InnerText} theo quy định." + "\n");
                            }
                            if (soluong != null && int.TryParse(soluong.InnerText.Trim(), out soluongValue))
                            {
                                // So sánh với số nguyên cụ thể, ví dụ so sánh với 0
                                if (soluongValue <= 0)
                                {
                                    errors.Add( "Số lượng không được nhỏ hơn 0." + "\n");
                                }
                            }
                            if (MA_BAC_SI1 != null && MA_BAC_SI1 != null && string.IsNullOrWhiteSpace(MA_BAC_SI1.InnerText.Trim()) && MA_NHOM.InnerText.Trim() != "15")
                            {
                                errors.Add("MA_BAC_SI không được để trống." + "\n");
                            }
                            if (MA_BAC_SI1 != null && MA_BAC_SI1 != null && MA_NHOM.InnerText.Trim() != "15")
                            {
                                var listMa = MA_BAC_SI1.InnerText.Trim().Split(';');
                                bool hasError = false;
                                foreach (var ma in listMa)
                                {
                                    if (!checkformat(ma.Trim(), @"^\d{6}/[A-Z]{2,4}-[A-Z]{3,5}$"))
                                    {
                                        hasError = true;
                                        break;
                                    }
                                }
                                if (hasError)
                                {
                                    errors.Add($"MA_BAC_SI {MA_BAC_SI1.InnerText}  chứa mã không đúng định dạng." + "\n");
                                }
                            }

                            if (NGUOI_THUC_HIEN != null && NGUOI_THUC_HIEN != null && string.IsNullOrWhiteSpace(NGUOI_THUC_HIEN.InnerText.Trim()) && MA_NHOM.InnerText.Trim() != "15")
                            {
                                errors.Add("NGUOI_THUC_HIEN không được để trống." + "\n");
                            }
                            if (NGUOI_THUC_HIEN != null && NGUOI_THUC_HIEN != null && MA_NHOM.InnerText.Trim() != "15")
                            {
                                var listMa = NGUOI_THUC_HIEN.InnerText.Trim().Split(';');
                                bool hasError = false;
                                foreach (var ma in listMa)
                                {
                                    if (!checkformat(ma.Trim(), @"^\d{6}/[A-Z]{2,4}-[A-Z]{3,5}$"))
                                    {
                                        hasError = true;
                                        break;
                                    }
                                }
                                if (hasError)
                                {
                                    errors.Add($"NGUOI_THUC_HIEN {NGUOI_THUC_HIEN.InnerText} chứa mã không đúng định dạng." + "\n");
                                }
                            }
                            if (NGAY_YL != null && NGAY_TH_YL != null &&
                           !string.IsNullOrWhiteSpace(NGAY_YL.InnerText) &&
                           !string.IsNullOrWhiteSpace(NGAY_TH_YL.InnerText))
                            {
                                if (!SosanhTime2(NGAY_YL.InnerText, NGAY_TH_YL.InnerText))
                                {
                                    errors.Add( "NGAY_TH_YL không được nhỏ hơn NGAY_YL." + "\n");
                                }
                            }
                            if (NGAY_YL != null && ngay_rv != null &&
                            !string.IsNullOrWhiteSpace(NGAY_YL.InnerText) &&
                            !string.IsNullOrWhiteSpace(ngay_rv))
                            {
                                if (!SosanhTime2(NGAY_YL.InnerText, ngay_rv))
                                {
                                    errors.Add( "NGAY_RA không được nhỏ hơn NGAY_YL." + "\n");
                                }
                            }
                            if (NGAY_YL != null && ngay_vv != null &&
                            !string.IsNullOrWhiteSpace(NGAY_YL.InnerText) &&
                            !string.IsNullOrWhiteSpace(ngay_vv))
                            {
                                if (!SosanhTime2(ngay_vv, NGAY_YL.InnerText))
                                {
                                    errors.Add( "NGAY_YL không được nhỏ hơn NGAY_VAO." + "\n");
                                }
                            }
                            if (NGAY_TH_YL != null && ngay_rv != null &&
                            !string.IsNullOrWhiteSpace(NGAY_TH_YL.InnerText) &&
                            !string.IsNullOrWhiteSpace(ngay_rv))
                            {
                                if (!SosanhTime2(NGAY_TH_YL.InnerText, ngay_rv))
                                {
                                    errors.Add( "NGAY_RA không được nhỏ hơn NGAY_TH_YL." + "\n");
                                }
                            }
                            if (NGAY_TH_YL != null && ngay_vv != null &&
                           !string.IsNullOrWhiteSpace(NGAY_TH_YL.InnerText) &&
                           !string.IsNullOrWhiteSpace(ngay_vv))
                            {
                                if (!SosanhTime2(ngay_vv, NGAY_TH_YL.InnerText))
                                {
                                    errors.Add( "NGAY_TH_YL không được nhỏ hơn NGAY_VAO." + "\n");
                                }
                            }
                            if (NGAY_KQ != null && ngay_vv != null &&
                          !string.IsNullOrWhiteSpace(NGAY_KQ.InnerText) &&
                          !string.IsNullOrWhiteSpace(ngay_vv))
                            {
                                if (!SosanhTime2(ngay_vv, NGAY_KQ.InnerText))
                                {
                                    errors.Add( "NGAY_KQ không được nhỏ hơn NGAY_VAO." + "\n");
                                }
                            }
                            if (NGAY_KQ != null && ngay_rv != null &&
                           !string.IsNullOrWhiteSpace(NGAY_KQ.InnerText) &&
                           !string.IsNullOrWhiteSpace(ngay_rv))
                            {
                                if (!SosanhTime2(NGAY_KQ.InnerText, ngay_rv))
                                {
                                    errors.Add( "NGAY_RA không được nhỏ hơn NGAY_KQ." + "\n");
                                }
                            }
                            if (NGAY_TH_YL != null && NGAY_KQ != null &&
                            !string.IsNullOrWhiteSpace(NGAY_TH_YL.InnerText) &&
                            !string.IsNullOrWhiteSpace(NGAY_KQ.InnerText))
                            {
                                if (!SosanhTime2(NGAY_TH_YL.InnerText, NGAY_KQ.InnerText))
                                {
                                    errors.Add( "NGAY_TH_YL không được nhỏ hơn NGAY_KQ." + "\n");
                                }
                            }
                            if (NGAY_KQ != null && ngaythanhtoan != null &&
                           !string.IsNullOrWhiteSpace(NGAY_KQ.InnerText) &&
                           !string.IsNullOrWhiteSpace(ngaythanhtoan))
                            {
                                if (!SosanhTime2(NGAY_KQ.InnerText, ngaythanhtoan))
                                {
                                    errors.Add( "NGAY_TTOAN không được nhỏ hơn NGAY_KQ." + "\n");
                                }
                            }

                            if (MA_DICH_VU != null && NGAY_KQ != null && NGAY_KQ.InnerText.Trim() == "" && MA_DICH_VU.InnerText != "")
                            {
                                errors.Add( "NGAY_KQ không được để trống." + "\n");
                            }
                            if (MA_DICH_VU != null && TEN_DICH_VU != null && checkChar(TEN_DICH_VU.InnerText) && MA_DICH_VU.InnerText != "")
                            {
                                errors.Add($"TEN_DICH_VU {TEN_DICH_VU.InnerText} có định dạng không phải là Unicode tổ hợp." + "\n");
                            }
                            if (MA_VAT_TU != null && TEN_VAT_TU != null && checkChar(TEN_VAT_TU.InnerText) && MA_VAT_TU.InnerText != "")
                            {
                                errors.Add($"TEN_VAT_TU {TEN_VAT_TU.InnerText} có định dạng không phải là Unicode tổ hợp." + "\n");
                            }

                            if (MA_DICH_VU != null && MA_DICH_VU.InnerText != "")
                            {
                                if (int.TryParse(MA_NHOM.InnerText.Trim(), out int maNhomValue))
                                {
                                    if (SosanhTime(NGAY_KQ.InnerText.Trim(), NGAY_TH_YL.InnerText.Trim(), 5) == true && maNhomValue == 2)
                                    {
                                        errors.Add( "NGAY_TH_YL đến NGAY_KQ nhỏ hơn 5 phút." + "\n");
                                    }
                                }
                            }

                        }
                    }
                }
                if (strNode == "XML4")
                {
                    XmlNodeList nodeYL = xmlDoc.SelectNodes("//CHI_TIET_CLS");
                    if (nodeYL != null && nodeYL.Count > 0)
                    {
                        foreach (XmlNode chiTietNode in nodeYL)
                        {
                            XmlNode MA_DICH_VU = chiTietNode.SelectSingleNode("MA_DICH_VU");
                            XmlNode NGAY_KQ = chiTietNode.SelectSingleNode("NGAY_KQ");
                            XmlNode MA_BS_DOC_KQ = chiTietNode.SelectSingleNode("MA_BS_DOC_KQ");
                            XmlNode MO_TA = chiTietNode.SelectSingleNode("MO_TA");
                            if (MA_DICH_VU != null && string.IsNullOrWhiteSpace(MA_DICH_VU.InnerText.Trim()))
                            {
                                errors.Add( "MA_DICH_VU không được để trống." + "\n");
                            }
                            if (MA_BS_DOC_KQ != null && MA_BS_DOC_KQ != null && string.IsNullOrWhiteSpace(MA_BS_DOC_KQ.InnerText.Trim()))
                            {
                                errors.Add( "MA_BS_DOC_KQ không được để trống." + "\n");
                            }

                            if (NGAY_KQ != null && string.IsNullOrWhiteSpace(NGAY_KQ.InnerText.Trim()))
                            {
                                errors.Add( "NGAY_KQ không được để trống." + "\n");
                            }
                            if (MO_TA != null && !string.IsNullOrWhiteSpace(MO_TA.InnerText) && MO_TA.InnerText.Trim().Length > 4000)
                            {
                                errors.Add( "MO_TA không được vượt quá 4000 ký tự." + "\n");
                            }
                        }
                    }
                }
                if (strNode == "XML5")
                {
                    XmlNodeList nodeYL = xmlDoc.SelectNodes("//CHI_TIET_DIEN_BIEN_BENH");
                    if (nodeYL != null && nodeYL.Count > 0)
                    {
                        foreach (XmlNode chiTietNode in nodeYL)
                        {
                            XmlNode DIEN_BIEN_LS = chiTietNode.SelectSingleNode("DIEN_BIEN_LS");
                            XmlNode NGUOI_THUC_HIEN = chiTietNode.SelectSingleNode("NGUOI_THUC_HIEN");
                            XmlNode THOI_DIEM_DBLS = chiTietNode.SelectSingleNode("THOI_DIEM_DBLS");

                            if (DIEN_BIEN_LS != null && DIEN_BIEN_LS.InnerText.Trim() == "")
                            {
                                errors.Add("DIEN_BIEN_LS không được để trống." + "\n");
                            }
                            if (NGUOI_THUC_HIEN != null && NGUOI_THUC_HIEN.InnerText.Trim() == "")
                            {
                                errors.Add("NGUOI_THUC_HIEN không được để trống." + "\n");
                            }
                            if (THOI_DIEM_DBLS != null && THOI_DIEM_DBLS.InnerText.Trim() == "")
                            {
                                errors.Add( "THOI_DIEM_DBLS không được để trống." + "\n");
                            }



                        }
                    }
                }
                if (strNode == "XML7")
                {
                    XmlNodeList nodeYL = xmlDoc.SelectNodes("//CHI_TIEU_DU_LIEU_GIAY_RA_VIEN");
                    if (nodeYL != null && nodeYL.Count > 0)
                    {
                        foreach (XmlNode chiTietNode in nodeYL)
                        {
                            XmlNode PP_DIEUTRI = chiTietNode.SelectSingleNode("PP_DIEUTRI");
                            XmlNode CHAN_DOAN_RV = chiTietNode.SelectSingleNode("CHAN_DOAN_RV");
                            XmlNode NGAY_VAO = chiTietNode.SelectSingleNode("NGAY_VAO");
                            XmlNode NGAY_RA = chiTietNode.SelectSingleNode("NGAY_RA");
                            XmlNode SO_LUU_TRU = chiTietNode.SelectSingleNode("SO_LUU_TRU");
                            XmlNode MA_BS = chiTietNode.SelectSingleNode("MA_BS");
                            XmlNode ma_ttdv = chiTietNode.SelectSingleNode("//MA_TTDV");

                            if (MA_BS != null && MA_BS.InnerText.Trim() == "")
                            {
                                errors.Add( "MA_BS không được để trống." + "\n");
                            }
                            if (PP_DIEUTRI != null && PP_DIEUTRI.InnerText.Trim() == "")
                            {
                                errors.Add( "PP_DIEUTRI không được để trống." + "\n");
                            }
                            if (CHAN_DOAN_RV != null && CHAN_DOAN_RV.InnerText.Trim() == "")
                            {
                                errors.Add( "CHAN_DOAN_RV không được để trống." + "\n");
                            }
                            if (SO_LUU_TRU != null && SO_LUU_TRU.InnerText.Trim() == "")
                            {
                                errors.Add( "SO_LUU_TRU không được để trống." + "\n");
                            }
                            if (NGAY_VAO != null && NGAY_VAO.InnerText.Trim() == "")
                            {
                                errors.Add( "NGAY_VAO không được để trống." + "\n");
                            }
                            if (NGAY_RA != null && NGAY_RA.InnerText.Trim() == "")
                            {
                                errors.Add( "NGAY_RA không được để trống." + "\n");
                            }
                            if (ma_ttdv != null && (ma_ttdv.InnerText != mattdv_thayHung && ma_ttdv.InnerText != mattdv_thayTrung))
                            {
                                errors.Add( "Thông tin MA_TTDV sai." + "\n");
                            }
                            if (ma_ttdv.InnerText == "")
                            {
                                errors.Add( "Thông tin MA_TTDV không được để trống." + "\n");
                            }
                        }
                    }
                }
                if (strNode == "XML8")
                {
                    XmlNodeList nodeYL = xmlDoc.SelectNodes("//CHI_TIEU_DU_LIEU_TOM_TAT_HO_SO_BENH_AN");
                    if (nodeYL != null && nodeYL.Count > 0)
                    {
                        foreach (XmlNode chiTietNode in nodeYL)
                        {
                            XmlNode PP_DIEUTRI = chiTietNode.SelectSingleNode("PP_DIEUTRI");
                            XmlNode CHAN_DOAN_RV = chiTietNode.SelectSingleNode("CHAN_DOAN_RV");
                            XmlNode CHAN_DOAN_VAO = chiTietNode.SelectSingleNode("CHAN_DOAN_VAO");
                            XmlNode NGAY_VAO = chiTietNode.SelectSingleNode("NGAY_VAO");
                            XmlNode NGAY_RA = chiTietNode.SelectSingleNode("NGAY_RA");
                            XmlNode TOMTAT_KQ = chiTietNode.SelectSingleNode("TOMTAT_KQ");
                            XmlNode QT_BENHLY = chiTietNode.SelectSingleNode("QT_BENHLY");
                            XmlNode ma_ttdv = chiTietNode.SelectSingleNode("//MA_TTDV");

                            if (QT_BENHLY != null && QT_BENHLY.InnerText.Trim() == "")
                            {
                                errors.Add( "QT_BENHLY không được để trống." + "\n");
                            }
                            if (PP_DIEUTRI != null && PP_DIEUTRI.InnerText.Trim() == "")
                            {
                                errors.Add( "PP_DIEUTRI không được để trống." + "\n");
                            }
                            if (CHAN_DOAN_VAO != null && CHAN_DOAN_VAO.InnerText.Trim() == "")
                            {
                                errors.Add( "CHAN_DOAN_VAO không được để trống." + "\n");
                            }
                            if (CHAN_DOAN_RV != null && CHAN_DOAN_RV.InnerText.Trim() == "")
                            {
                                errors.Add( "CHAN_DOAN_RV không được để trống." + "\n");
                            }
                            if (TOMTAT_KQ != null && !string.IsNullOrWhiteSpace(TOMTAT_KQ.InnerText) && TOMTAT_KQ.InnerText.Trim().Length > 4000)
                            {
                                errors.Add( "TOMTAT_KQ không được vượt quá 4000 ký tự." + "\n");
                            }
                            if (NGAY_VAO != null && NGAY_VAO.InnerText.Trim() == "")
                            {
                                errors.Add( "NGAY_VAO không được để trống." + "\n");
                            }
                            if (NGAY_RA != null && NGAY_RA.InnerText.Trim() == "")
                            {
                                errors.Add( "NGAY_RA không được để trống." + "\n");
                            }
                            if (ma_ttdv != null && (ma_ttdv.InnerText != mattdv_thayHung && ma_ttdv.InnerText != mattdv_thayTrung))
                            {
                                errors.Add($"Thông tin MA_TTDV {ma_ttdv.InnerText} sai." + "\n");
                            }
                        }
                    }
                }

                if (strNode == "XML11")
                {
                    XmlNodeList nodeYL = xmlDoc.SelectNodes("//CHI_TIEU_DU_LIEU_GIAY_CHUNG_NHAN_NGHI_VIEC_HUONG_BAO_HIEM_XA_HOI");
                    if (nodeYL != null && nodeYL.Count > 0)
                    {
                        foreach (XmlNode chiTietNode in nodeYL)
                        {
                            XmlNode MA_BS = chiTietNode.SelectSingleNode("MA_BS");
                            XmlNode CHAN_DOAN_RV = chiTietNode.SelectSingleNode("CHAN_DOAN_RV");
                            XmlNode PP_DIEUTRI = chiTietNode.SelectSingleNode("PP_DIEUTRI");
                            XmlNode ma_ttdv = chiTietNode.SelectSingleNode("//MA_TTDV");
                            XmlNode MA_BHXH = chiTietNode.SelectSingleNode("MA_BHXH");
                            XmlNode MA_THE_BHYT = chiTietNode.SelectSingleNode("//MA_THE_BHYT");

                            if (MA_BS != null && MA_BS.InnerText.Trim() == "")
                            {
                                errors.Add( "MA_BS không được để trống." + "\n");
                            }

                            if (CHAN_DOAN_RV != null && CHAN_DOAN_RV.InnerText.Trim() == "")
                            {
                                errors.Add( "CHAN_DOAN_RV không được để trống." + "\n");
                            }

                            if (PP_DIEUTRI != null && PP_DIEUTRI.InnerText.Trim() == "")
                            {
                                errors.Add( "PP_DIEUTRI không được để trống." + "\n");
                            }
                            if (MA_BHXH != null && MA_BHXH.InnerText.Trim() == "")
                            {
                                errors.Add( "MA_BHXH không được để trống." + "\n");
                            }
                            if (ma_ttdv != null && (ma_ttdv.InnerText != mattdv_thayHung && ma_ttdv.InnerText != mattdv_thayTrung))
                            {
                                errors.Add($"Thông tin MA_TTDV {ma_ttdv.InnerText} sai." + "\n");
                            }
                            if (MA_THE_BHYT != null && MA_THE_BHYT.InnerText.Trim() == "")
                            {
                                errors.Add( "Thông tin MA_THE_BHYT không được để trống." + "\n");
                            }
                        }
                    }
                }

                if (strNode == "XML14")
                {
                    XmlNodeList nodeYL = xmlDoc.SelectNodes("//CHI_TIEU_GIAYHEN_KHAMLAI");
                    if (nodeYL != null && nodeYL.Count > 0)
                    {
                        foreach (XmlNode chiTietNode in nodeYL)
                        {
                            XmlNode MA_BAC_SI = chiTietNode.SelectSingleNode("MA_BAC_SI");
                            XmlNode CHAN_DOAN_RV = chiTietNode.SelectSingleNode("CHAN_DOAN_RV");
                            XmlNode NGAY_VAO = chiTietNode.SelectSingleNode("NGAY_VAO");
                            XmlNode NGAY_RA = chiTietNode.SelectSingleNode("NGAY_RA");
                            XmlNode ma_ttdv = chiTietNode.SelectSingleNode("//MA_TTDV");
                            if (MA_BAC_SI != null && MA_BAC_SI.InnerText.Trim() == "")
                            {
                                errors.Add( "MA_BAC_SI không được để trống." + "\n");
                            }

                            if (CHAN_DOAN_RV != null && CHAN_DOAN_RV.InnerText.Trim() == "")
                            {
                                errors.Add( "CHAN_DOAN_RV không được để trống." + "\n");
                            }

                            if (NGAY_VAO != null && NGAY_VAO.InnerText.Trim() == "")
                            {
                                errors.Add( "NGAY_VAO không được để trống." + "\n");
                            }
                            if (NGAY_RA != null && NGAY_RA.InnerText.Trim() == "")
                            {
                                errors.Add( "NGAY_RA không được để trống." + "\n");
                            }
                            if (ma_ttdv != null && (ma_ttdv.InnerText != mattdv_thayHung && ma_ttdv.InnerText != mattdv_thayTrung))
                            {
                                errors.Add( $"Thông tin MA_TTDV {ma_ttdv.InnerText} sai." + "\n");
                            }
                        }
                    }
                }
                // Apply similar changes to XML4, XML5, XML7, XML8, XML11, XML14
            }
            catch (Exception ex)
            {
                errors.Add($"Lỗi xử lý XML {strNode}: {ex.Message}");
            }

            return (tenBN_string, maBN_string, errors);
        }





    }
}