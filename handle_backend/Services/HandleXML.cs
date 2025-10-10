using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
//using Npgsql;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Collections;
using System.Data.SqlTypes;
using System.Xml;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace handle_backend.Services
{
    public class HandleXML
    {
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

            return  input.Trim();
        }
        public bool SosanhTime(string Datetime1, string Datetime2, int sophut)
        {
            DateTime time1, time2;

            // Kiểm tra và chuyển đổi định dạng
            bool isValid1 = DateTime.TryParseExact(Datetime1, "yyyyMMddHHmm", null, DateTimeStyles.None, out time1);
            bool isValid2 = DateTime.TryParseExact(Datetime2, "yyyyMMddHHmm", null, DateTimeStyles.None, out time2);

            if (!isValid1 || !isValid2)
            {
                // Không đúng định dạng
                return  false;
            }

            TimeSpan chenhLech = (time1 - time2).Duration(); // khoảng cách tuyệt đối
            return  chenhLech < TimeSpan.FromMinutes(sophut);

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
                    return  true;
                }
            }
            return  false;
        }
    
        public bool SosanhTime2(string Datetime1, string Datetime2)
        {
            // Kiểm tra nếu một trong hai chuỗi rỗng hoặc null
            if (string.IsNullOrWhiteSpace(Datetime1) || string.IsNullOrWhiteSpace(Datetime2))
            {
                getError +=  false;
            }

            // Khai báo định dạng
            string format = "yyyyMMddHHmm";
            DateTime time1, time2;

            // Dùng TryParseExact để kiểm tra định dạng và parse an toàn
            bool isValidTime1 = DateTime.TryParseExact(Datetime1, format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None, out time1);

            bool isValidTime2 = DateTime.TryParseExact(Datetime2, format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None, out time2);

            // Nếu không đúng định dạng thì báo lỗi
            if (!isValidTime1 || !isValidTime2)
            {
                return  false;
            }
            if (time1 <= time2)
            {
                return  true;
            }
            else
            {
                return  false;
            }
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

        string getError = "";
        private void SaveAndCheck(string strNode, string decodedString)
        {
            string mattdv = "2997032342";
            int soluongValue = 0;
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(decodedString);
            XmlNode malk = xmlDoc.SelectSingleNode("//MA_LK");
            string ngay_vv = "";
            string ngay_rv = "";
            string ngaythanhtoan = "";
            //string sql = "";
            if (strNode == "XML1")
            {
                XmlNodeList nodeYL = xmlDoc.SelectNodes("//TONG_HOP");

                if (nodeYL != null && nodeYL.Count > 0)
                {
                    foreach (XmlNode chiTietNode in nodeYL)
                    {
                        XmlNode mabn = chiTietNode.SelectSingleNode("//MA_BN");
                        XmlNode hotenbn = chiTietNode.SelectSingleNode("//HO_TEN");
                        XmlNode socccd = chiTietNode.SelectSingleNode("//SO_CCCD");
                        XmlNode ngaysinh = chiTietNode.SelectSingleNode("//NGAY_SINH");
                        XmlNode gioitinh = chiTietNode.SelectSingleNode("//GIOI_TINH");
                        XmlNode diachi = chiTietNode.SelectSingleNode("//DIA_CHI");
                        XmlNode mathebhyt = chiTietNode.SelectSingleNode("//MA_THE_BHYT");
                        XmlNode ngayvaonoitru = chiTietNode.SelectSingleNode("//NGAY_VAO_NOI_TRU");
                        XmlNode ngayttoan = chiTietNode.SelectSingleNode("//NGAY_TTOAN");
                        XmlNode ma_ttdv = 
                            
                            
                            chiTietNode.SelectSingleNode("//MA_TTDV");
                        XmlNode namqt = chiTietNode.SelectSingleNode("//NAM_QT");
                        XmlNode thangqt = chiTietNode.SelectSingleNode("//THANG_QT");
                        XmlNode maloaikcb = chiTietNode.SelectSingleNode("//MA_LOAI_KCB");
                        XmlNode makhoa = chiTietNode.SelectSingleNode("//MA_KHOA");
                        XmlNode macskcb = chiTietNode.SelectSingleNode("//MA_CSKCB");
                        XmlNode giatritu = chiTietNode.SelectSingleNode("//GT_THE_TU");
                        XmlNode giatriden = chiTietNode.SelectSingleNode("//GT_THE_DEN");
                        XmlNode ngayvao = chiTietNode.SelectSingleNode("//NGAY_VAO");
                        XmlNode ngayra = chiTietNode.SelectSingleNode("//NGAY_RA");
                        XmlNode lydovnt = chiTietNode.SelectSingleNode("//LY_DO_VNT");
                        XmlNode lydovv = chiTietNode.SelectSingleNode("//LY_DO_VV");
                        XmlNode mabenhchinh = chiTietNode.SelectSingleNode("//MA_BENH_CHINH");
                        XmlNode chandoanrv = chiTietNode.SelectSingleNode("//CHAN_DOAN_RV");
                        XmlNode chandoanvv = chiTietNode.SelectSingleNode("//CHAN_DOAN_VAO");
                        XmlNode maquoctich = chiTietNode.SelectSingleNode("//MA_QUOCTICH");
                        XmlNode ppdieutri = chiTietNode.SelectSingleNode("//PP_DIEU_TRI");
                        XmlNode MA_NGHE_NGHIEP = chiTietNode.SelectSingleNode("//MA_NGHE_NGHIEP");
                        ngay_rv = ngayra.InnerText.Trim();
                        ngay_vv = ngayvao.InnerText.Trim();
                        ngaythanhtoan = ngayttoan.InnerText.Trim();
                       getError +=  "Hồ sơ Bệnh nhân: " + hotenbn.InnerText + " - " + malk.InnerText + " - " + mabn.InnerText + " - " + MA_NGHE_NGHIEP.InnerText + " \n ";
                        if (lydovv != null && lydovv.InnerText == "")
                        {
                           getError +=  "Trường LY_DO_VV không được để trống" + "\n";
                        }
                        if (ppdieutri != null && ppdieutri.InnerText == "")
                        {
                           getError +=  "Trường PP_DIEU_TRI không được để trống" + "\n";
                        }
                        if (mabenhchinh != null && mabenhchinh.InnerText == "")
                        {
                           getError +=  "Trường mã bệnh chính MA_BENH_CHINH không được để trống" + "\n";
                        }
                        if (chandoanvv != null && chandoanvv.InnerText == "")
                        {
                           getError +=  "Trường chẩn đoán vào viện CHAN_DOAN_VAO không được để trống" + "\n";
                        }
                        if (chandoanrv != null && string.IsNullOrWhiteSpace(chandoanrv.InnerText.Trim()))
                        {
                           getError +=  "Trường chẩn đoán ra viện CHAN_DOAN_RV không được để trống" + "\n";
                        }
                        if (MA_NGHE_NGHIEP != null && string.IsNullOrWhiteSpace(MA_NGHE_NGHIEP.InnerText.Trim()))
                        {
                           getError +=  "MA_NGHE_NGHIEP không được để trống" + "\n";
                        }
                        if (maquoctich != null && maquoctich.InnerText != "000")
                        {
                           getError +=  "Sai quốc tịch." + "\n";
                        }
                        if (lydovnt != null && lydovnt.InnerText == "" && maloaikcb.InnerText == "3")
                        {
                           getError +=  "Trường LY_DO_VNT không được để trống" + "\n";
                        }
                        if (ngayra != null && ngayttoan != null && SosanhTime2(ngayra.InnerText, ngayttoan.InnerText) == false)
                        {
                           getError +=  "Ngày thanh toán không được nhỏ hơn ngày ra viện." + "\n";
                        }
                        if (ngayra != null && ngayvao != null && SosanhTime2(ngayvao.InnerText, ngayra.InnerText) == false)
                        {
                           getError +=  "Ngày ra không được nhỏ hơn ngày vào viện" + "\n";
                        }
                        if (ngayttoan != null && ngayvao != null && SosanhTime2(ngayvao.InnerText, ngayttoan.InnerText) == false)
                        {
                           getError +=  "Ngày thanh toán không được nhỏ hơn ngày vào viện." + "\n";

                        }
                        if (giatritu != null && giatriden != null && SosanhTime2(giatritu.InnerText + "0000", giatriden.InnerText + "0000") == false)
                        {
                           getError +=  "Hạn thẻ không đúng, đến ngày nhỏ hơn từ ngày." + "\n";
                        }
                        if (namqt != null && namqt.InnerText == "")
                        {
                           getError +=  "Năm quyết toán không được để trống." + "\n";
                        }
                        if (thangqt != null && thangqt.InnerText == "")
                        {
                           getError +=  "Tháng quyết toán không được để trống." + "\n";
                        }
                        if (socccd != null && checkformat(socccd.InnerText, @"^\d{12}$") == false && socccd.InnerText != "")
                        {
                           getError +=  "Thẻ căn cước không đúng định dạng." + "\n";
                        }
                        if (ma_ttdv != null && ma_ttdv.InnerText != mattdv)
                        {
                           getError +=  "Thông tin MA_TTDV sai." + "\n";
                        }
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
                        if (mathuoc != null && checkformat(mathuoc.InnerText.Trim(), @"^(\d+(\.\d+){1,2}|\d+[A-Za-z]\.\d+)$") == false)
                        {
                           getError +=  "Sai định dạng mã thuốc MA_THUOC theo quy định" + "\n";
                        }
                        string input = CleanInput(lieudung.InnerText.Trim());
                        string inputOriginal = lieudung.InnerText.Trim();

                        if (mathuoc != null && mathuoc.InnerText != "40.17")
                        {
                            bool isFormatCorrect = checkformat(input, @"^(\d+(/\d+)?|\d*\.?\d+)\s*\w+(?:\s\w+)*\s*/\s*lần\s*\*\s*(\d+(/\d+)?|\d*\.?\d+)\s*lần/ngày\s*\*\s*(\d+(/\d+)?|\d*\.?\d+)\s*ngày\s*\[\s*(\d+(/\d+)?|\d*\.?\d+)\s*\w+(?:\s\w+)*\s*/\s*ngày\s*\]$");

                            bool isValid = Regex.IsMatch(inputOriginal, @"^(?:(Sáng|Trưa|Chiều|Tối):\s*\d*\.?\d+\s*\w+\s*,\s*)*(Sáng|Trưa|Chiều|Tối):\s*\d*\.?\d+\s*\w+\s*(\*\s*\d+\s*ngày)?\s*\[\s*\d*\.?\d+\s*\w+\s*/ngày\s*\]$", RegexOptions.IgnoreCase);

                            if (!isFormatCorrect && !isValid)
                            {
                               getError +=  "Sai định dạng LIEU_DUNG theo quy định." + "\n";
                            }
                        }
                        else
                        {
                            bool isFormatCorrect = checkformat(input, @"^(\d+(/\d+)?|\d*\.?\d+)\s*\w+(?:\s\w+)*\s*/\s*lần\s*\*\s*(\d+(/\d+)?|\d*\.?\d+)\s*lần/ngày\s*\*\s*(\d+(/\d+)?|\d*\.?\d+)\s*ngày\s*\[\s*(\d+(/\d+)?|\d*\.?\d+)\s*\w+(?:\s\w+)*\s*/\s*ngày\s*\]$");

                            bool isValid = Regex.IsMatch(inputOriginal, @"^(?:(Sáng|Trưa|Chiều|Tối):\s*\d*\.?\d+\s*\w+\s*,\s*)*(Sáng|Trưa|Chiều|Tối):\s*\d*\.?\d+\s*\w+\s*(\*\s*\d+\s*ngày)?\s*\[\s*\d*\.?\d+\s*\w+\s*/ngày\s*\]$", RegexOptions.IgnoreCase);

                            if (!isFormatCorrect && !isValid)
                            {
                               getError +=  "Sai định dạng LIEU_DUNG theo quy định." + "\n";
                            }
                        }
                        if (ttthau != null && ttthau != null && checkformat(ttthau.InnerText.Trim(), @"^[\p{L}0-9/_-]+;[A-Z0-9]+;[A-Z0-9]+;\d{4}$") == false && mathuoc.InnerText != "40.17")
                        {
                            if (string.IsNullOrEmpty(ttthau.InnerText))
                            {
                               getError +=  "Thông tin thầu TT_THAU không được để trống." + "\n";
                            }
                            else
                            {
                               getError +=  "Sai thông tin thầu TT_THAU theo quy định." + "\n";
                            }
                        }
                        if (soluong != null && int.TryParse(soluong.InnerText.Trim(), out soluongValue))
                        {
                            // So sánh với số nguyên cụ thể, ví dụ so sánh với 0
                            if (soluongValue <= 0)
                            {
                               getError +=  "Số lượng không được nhỏ hơn 0." + "\n";
                            }
                        }
                        if (MA_BAC_SI != null && string.IsNullOrWhiteSpace(MA_BAC_SI.InnerText.Trim()))
                        {
                           getError +=  "Mã chứng chỉ hành nghề của bác sỹ không được để trống." + "\n";
                        }
                        if (MA_BAC_SI != null && checkformat(MA_BAC_SI.InnerText.Trim(), @"^\d{6}/[A-Z]{2,3}-[A-Z]{4}$") == false)
                        {
                           getError +=  "Mã chứng chỉ hành nghề của bác sỹ không đúng định dạng." + "\n";
                        }
                        if (NGAY_YL != null && NGAY_TH_YL != null &&
                      !string.IsNullOrWhiteSpace(NGAY_YL.InnerText) &&
                      !string.IsNullOrWhiteSpace(NGAY_TH_YL.InnerText))
                        {
                            if (!SosanhTime2(NGAY_YL.InnerText, NGAY_TH_YL.InnerText))
                            {
                               getError +=  "NGAY_TH_YL không được nhỏ hơn NGAY_YL." + "\n";
                            }
                        }
                        if (NGAY_YL != null && ngay_rv != null &&
                        !string.IsNullOrWhiteSpace(NGAY_YL.InnerText) &&
                        !string.IsNullOrWhiteSpace(ngay_rv))
                        {
                            if (SosanhTime2(NGAY_YL.InnerText, ngay_rv) == false)
                            {
                               getError +=  "NGAY_RA không được nhỏ hơn NGAY_YL." + "\n";
                            }
                        }
                        if (NGAY_YL != null && ngay_vv != null &&
                        !string.IsNullOrWhiteSpace(NGAY_YL.InnerText) &&
                        !string.IsNullOrWhiteSpace(ngay_vv))
                        {
                            if (!SosanhTime2(ngay_vv, NGAY_YL.InnerText))
                            {
                               getError +=  "NGAY_YL không được nhỏ hơn NGAY_VAO." + "\n";
                            }
                        }
                        if (NGAY_TH_YL != null && ngay_rv != null &&
                        !string.IsNullOrWhiteSpace(NGAY_TH_YL.InnerText) &&
                        !string.IsNullOrWhiteSpace(ngay_rv))
                        {
                            if (SosanhTime2(NGAY_TH_YL.InnerText, ngay_rv) == false)
                            {
                               getError +=  "NGAY_RA không được nhỏ hơn NGAY_TH_YL." + "\n";
                            }
                        }
                        if (NGAY_TH_YL != null && ngay_vv != null &&
                       !string.IsNullOrWhiteSpace(NGAY_TH_YL.InnerText) &&
                       !string.IsNullOrWhiteSpace(ngay_vv))
                        {
                            if (!SosanhTime2(ngay_vv, NGAY_TH_YL.InnerText))
                            {
                               getError +=  "NGAY_TH_YL không được nhỏ hơn NGAY_VAO." + "\n";
                            }
                        }
                        if (SO_DANG_KY != null && SO_DANG_KY.InnerText.Trim() == "" && mathuoc.InnerText != "40.17")
                        {
                           getError +=  "SO_DANG_KY không được để trống." + "\n";
                        }
                        if (TEN_THUOC != null && checkChar(TEN_THUOC.InnerText) && mathuoc.InnerText != "")
                        {
                           getError +=  "TEN_THUOC có định dạng không phải là Unicode tổ hợp." + "\n";
                        }
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
                               getError +=  "Sai định dạng mã thuốc MA_DICH_VU theo quy định" + "\n";
                            }
                        }
                        if (MA_VAT_TU != null && MA_VAT_TU.InnerText != "")
                        {
                            if (checkformat(MA_VAT_TU.InnerText.Trim(), @"^[A-Z]\d{2}\.\d{2}\.\d{3}\.\d{4}\.\d{3}\.\d{4}$") == false)
                            {
                               getError +=  "Sai định dạng mã thuốc MA_DICH_VU theo quy định" + "\n";
                            }
                        }

                        if (ttthau != null && MA_VAT_TU != null && checkformat(ttthau.InnerText.Trim(), @"^[\p{L}0-9/-]+;[\p{L}0-9]+;[\p{L}0-9]+;\d{4}$") == false &&
                            ttthau != null &&
                       !string.IsNullOrWhiteSpace(ttthau.InnerText) && MA_VAT_TU.InnerText != "")
                        {
                            if (ttthau.InnerText == "")
                            {
                               getError +=  "Thông tin thầu TT_THAU không được để trống." + "\n";
                            }
                            else
                            {
                               getError +=  "Sai thông tin thầu TT_THAU theo quy định." + "\n";
                            }

                        }
                        if (MA_MAY != null && checkformat(MA_MAY.InnerText.Trim(), @"^[A-ZĐ]{2,4}\d?\.\d+\.\d+\.[A-Z0-9]+$") == false &&
                            MA_MAY != null && !string.IsNullOrWhiteSpace(MA_MAY.InnerText.Trim()) && MA_DICH_VU.InnerText.Trim() != "")
                        {
                           getError +=  "Sai thông tin MA_MAY theo quy định." + "\n";
                        }
                        if (soluong != null && int.TryParse(soluong.InnerText.Trim(), out soluongValue))
                        {
                            // So sánh với số nguyên cụ thể, ví dụ so sánh với 0
                            if (soluongValue <= 0)
                            {
                               getError +=  "Số lượng không được nhỏ hơn 0." + "\n";
                            }
                        }
                        if (MA_BAC_SI1 != null && MA_BAC_SI1 != null && string.IsNullOrWhiteSpace(MA_BAC_SI1.InnerText.Trim()) && MA_NHOM.InnerText.Trim() != "15")
                        {
                           getError +=  "MA_BAC_SI không được để trống." + "\n";
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
                               getError +=  "MA_BAC_SI chứa mã không đúng định dạng." + "\n";
                            }
                        }

                        if (NGUOI_THUC_HIEN != null && NGUOI_THUC_HIEN != null && string.IsNullOrWhiteSpace(NGUOI_THUC_HIEN.InnerText.Trim()) && MA_NHOM.InnerText.Trim() != "15")
                        {
                           getError +=  "NGUOI_THUC_HIEN không được để trống." + "\n";
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
                               getError +=  "NGUOI_THUC_HIEN chứa mã không đúng định dạng." + "\n";
                            }
                        }
                        if (NGAY_YL != null && NGAY_TH_YL != null &&
                       !string.IsNullOrWhiteSpace(NGAY_YL.InnerText) &&
                       !string.IsNullOrWhiteSpace(NGAY_TH_YL.InnerText))
                        {
                            if (!SosanhTime2(NGAY_YL.InnerText, NGAY_TH_YL.InnerText))
                            {
                               getError +=  "NGAY_TH_YL không được nhỏ hơn NGAY_YL." + "\n";
                            }
                        }
                        if (NGAY_YL != null && ngay_rv != null &&
                        !string.IsNullOrWhiteSpace(NGAY_YL.InnerText) &&
                        !string.IsNullOrWhiteSpace(ngay_rv))
                        {
                            if (!SosanhTime2(NGAY_YL.InnerText, ngay_rv))
                            {
                               getError +=  "NGAY_RA không được nhỏ hơn NGAY_YL." + "\n";
                            }
                        }
                        if (NGAY_YL != null && ngay_vv != null &&
                        !string.IsNullOrWhiteSpace(NGAY_YL.InnerText) &&
                        !string.IsNullOrWhiteSpace(ngay_vv))
                        {
                            if (!SosanhTime2(ngay_vv, NGAY_YL.InnerText))
                            {
                               getError +=  "NGAY_YL không được nhỏ hơn NGAY_VAO." + "\n";
                            }
                        }
                        if (NGAY_TH_YL != null && ngay_rv != null &&
                        !string.IsNullOrWhiteSpace(NGAY_TH_YL.InnerText) &&
                        !string.IsNullOrWhiteSpace(ngay_rv))
                        {
                            if (!SosanhTime2(NGAY_TH_YL.InnerText, ngay_rv))
                            {
                               getError +=  "NGAY_RA không được nhỏ hơn NGAY_TH_YL." + "\n";
                            }
                        }
                        if (NGAY_TH_YL != null && ngay_vv != null &&
                       !string.IsNullOrWhiteSpace(NGAY_TH_YL.InnerText) &&
                       !string.IsNullOrWhiteSpace(ngay_vv))
                        {
                            if (!SosanhTime2(ngay_vv, NGAY_TH_YL.InnerText))
                            {
                               getError +=  "NGAY_TH_YL không được nhỏ hơn NGAY_VAO." + "\n";
                            }
                        }
                        if (NGAY_KQ != null && ngay_vv != null &&
                      !string.IsNullOrWhiteSpace(NGAY_KQ.InnerText) &&
                      !string.IsNullOrWhiteSpace(ngay_vv))
                        {
                            if (!SosanhTime2(ngay_vv, NGAY_KQ.InnerText))
                            {
                               getError +=  "NGAY_KQ không được nhỏ hơn NGAY_VAO." + "\n";
                            }
                        }
                        if (NGAY_KQ != null && ngay_rv != null &&
                       !string.IsNullOrWhiteSpace(NGAY_KQ.InnerText) &&
                       !string.IsNullOrWhiteSpace(ngay_rv))
                        {
                            if (!SosanhTime2(NGAY_KQ.InnerText, ngay_rv))
                            {
                               getError +=  "NGAY_RA không được nhỏ hơn NGAY_KQ." + "\n";
                            }
                        }
                        if (NGAY_TH_YL != null && NGAY_KQ != null &&
                        !string.IsNullOrWhiteSpace(NGAY_TH_YL.InnerText) &&
                        !string.IsNullOrWhiteSpace(NGAY_KQ.InnerText))
                        {
                            if (!SosanhTime2(NGAY_TH_YL.InnerText, NGAY_KQ.InnerText))
                            {
                               getError +=  "NGAY_TH_YL không được nhỏ hơn NGAY_KQ." + "\n";
                            }
                        }
                        if (NGAY_KQ != null && ngaythanhtoan != null &&
                       !string.IsNullOrWhiteSpace(NGAY_KQ.InnerText) &&
                       !string.IsNullOrWhiteSpace(ngaythanhtoan))
                        {
                            if (!SosanhTime2(NGAY_KQ.InnerText, ngaythanhtoan))
                            {
                               getError +=  "NGAY_TTOAN không được nhỏ hơn NGAY_KQ." + "\n";
                            }
                        }

                        if (MA_DICH_VU != null && NGAY_KQ != null && NGAY_KQ.InnerText.Trim() == "" && MA_DICH_VU.InnerText != "")
                        {
                           getError +=  "NGAY_KQ không được để trống." + "\n";
                        }
                        if (MA_DICH_VU != null && TEN_DICH_VU != null && checkChar(TEN_DICH_VU.InnerText) && MA_DICH_VU.InnerText != "")
                        {
                           getError +=  "TEN_DICH_VU có định dạng không phải là Unicode tổ hợp." + "\n";
                        }
                        if (MA_VAT_TU != null && TEN_VAT_TU != null && checkChar(TEN_VAT_TU.InnerText) && MA_VAT_TU.InnerText != "")
                        {
                           getError +=  "TEN_DICH_VU có định dạng không phải là Unicode tổ hợp." + "\n";
                        }

                        if (MA_DICH_VU != null && MA_DICH_VU.InnerText != "")
                        {
                            if (int.TryParse(MA_NHOM.InnerText.Trim(), out int maNhomValue))
                            {
                                if (SosanhTime(NGAY_KQ.InnerText.Trim(), NGAY_TH_YL.InnerText.Trim(), 5) == true && maNhomValue == 2)
                                {
                                   getError +=  "NGAY_TH_YL đến NGAY_KQ nhỏ hơn 5 phút." + "\n";
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
                           getError +=  "MA_DICH_VU không được để trống." + "\n";
                        }
                        if (MA_BS_DOC_KQ != null && MA_BS_DOC_KQ != null && string.IsNullOrWhiteSpace(MA_BS_DOC_KQ.InnerText.Trim()))
                        {
                           getError +=  "MA_BS_DOC_KQ không được để trống." + "\n";
                        }

                        if (NGAY_KQ != null && string.IsNullOrWhiteSpace(NGAY_KQ.InnerText.Trim()))
                        {
                           getError +=  "NGAY_KQ không được để trống." + "\n";
                        }
                        if (MO_TA != null && !string.IsNullOrWhiteSpace(MO_TA.InnerText) && MO_TA.InnerText.Trim().Length > 4000)
                        {
                           getError +=  "MO_TA không được vượt quá 4000 ký tự." + "\n";
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
                           getError +=  "DIEN_BIEN_LS không được để trống." + "\n";
                        }
                        if (NGUOI_THUC_HIEN != null && NGUOI_THUC_HIEN.InnerText.Trim() == "")
                        {
                           getError +=  "NGUOI_THUC_HIEN không được để trống." + "\n";
                        }
                        if (THOI_DIEM_DBLS != null && THOI_DIEM_DBLS.InnerText.Trim() == "")
                        {
                           getError +=  "THOI_DIEM_DBLS không được để trống." + "\n";
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
                           getError +=  "MA_BS không được để trống." + "\n";
                        }
                        if (PP_DIEUTRI != null && PP_DIEUTRI.InnerText.Trim() == "")
                        {
                           getError +=  "PP_DIEUTRI không được để trống." + "\n";
                        }
                        if (CHAN_DOAN_RV != null && CHAN_DOAN_RV.InnerText.Trim() == "")
                        {
                           getError +=  "CHAN_DOAN_RV không được để trống." + "\n";
                        }
                        if (SO_LUU_TRU != null && SO_LUU_TRU.InnerText.Trim() == "")
                        {
                           getError +=  "SO_LUU_TRU không được để trống." + "\n";
                        }
                        if (NGAY_VAO != null && NGAY_VAO.InnerText.Trim() == "")
                        {
                           getError +=  "NGAY_VAO không được để trống." + "\n";
                        }
                        if (NGAY_RA != null && NGAY_RA.InnerText.Trim() == "")
                        {
                           getError +=  "NGAY_RA không được để trống." + "\n";
                        }
                        if (ma_ttdv != null && ma_ttdv.InnerText != mattdv)
                        {
                           getError +=  "Thông tin MA_TTDV sai." + "\n";
                        }
                        if (ma_ttdv.InnerText == "")
                        {
                           getError +=  "Thông tin MA_TTDV không được để trống." + "\n";
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
                           getError +=  "QT_BENHLY không được để trống." + "\n";
                        }
                        if (PP_DIEUTRI != null && PP_DIEUTRI.InnerText.Trim() == "")
                        {
                           getError +=  "PP_DIEUTRI không được để trống." + "\n";
                        }
                        if (CHAN_DOAN_VAO != null && CHAN_DOAN_VAO.InnerText.Trim() == "")
                        {
                           getError +=  "CHAN_DOAN_VAO không được để trống." + "\n";
                        }
                        if (CHAN_DOAN_RV != null && CHAN_DOAN_RV.InnerText.Trim() == "")
                        {
                           getError +=  "CHAN_DOAN_RV không được để trống." + "\n";
                        }
                        if (TOMTAT_KQ != null && !string.IsNullOrWhiteSpace(TOMTAT_KQ.InnerText) && TOMTAT_KQ.InnerText.Trim().Length > 4000)
                        {
                           getError +=  "TOMTAT_KQ không được vượt quá 4000 ký tự." + "\n";
                        }
                        if (NGAY_VAO != null && NGAY_VAO.InnerText.Trim() == "")
                        {
                           getError +=  "NGAY_VAO không được để trống." + "\n";
                        }
                        if (NGAY_RA != null && NGAY_RA.InnerText.Trim() == "")
                        {
                           getError +=  "NGAY_RA không được để trống." + "\n";
                        }
                        if (ma_ttdv != null && ma_ttdv.InnerText != mattdv)
                        {
                           getError +=  "Thông tin MA_TTDV sai." + "\n";
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
                           getError +=  "MA_BS không được để trống." + "\n";
                        }

                        if (CHAN_DOAN_RV != null && CHAN_DOAN_RV.InnerText.Trim() == "")
                        {
                           getError +=  "CHAN_DOAN_RV không được để trống." + "\n";
                        }

                        if (PP_DIEUTRI != null && PP_DIEUTRI.InnerText.Trim() == "")
                        {
                           getError +=  "PP_DIEUTRI không được để trống." + "\n";
                        }
                        if (MA_BHXH != null && MA_BHXH.InnerText.Trim() == "")
                        {
                           getError +=  "MA_BHXH không được để trống." + "\n";
                        }
                        if (ma_ttdv != null && ma_ttdv.InnerText != mattdv)
                        {
                           getError +=  "Thông tin MA_TTDV sai." + "\n";
                        }
                        if (MA_THE_BHYT != null && MA_THE_BHYT.InnerText.Trim() == "")
                        {
                           getError +=  "Thông tin MA_THE_BHYT không được để trống." + "\n";
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
                           getError +=  "MA_BAC_SI không được để trống." + "\n";
                        }

                        if (CHAN_DOAN_RV != null && CHAN_DOAN_RV.InnerText.Trim() == "")
                        {
                           getError +=  "CHAN_DOAN_RV không được để trống." + "\n";
                        }

                        if (NGAY_VAO != null && NGAY_VAO.InnerText.Trim() == "")
                        {
                           getError +=  "NGAY_VAO không được để trống." + "\n";
                        }
                        if (NGAY_RA != null && NGAY_RA.InnerText.Trim() == "")
                        {
                           getError +=  "NGAY_RA không được để trống." + "\n";
                        }
                        if (ma_ttdv != null && ma_ttdv.InnerText != mattdv)
                        {
                           getError +=  "Thông tin MA_TTDV sai." + "\n";
                        }
                    }
                }
            }
        }

    }
}