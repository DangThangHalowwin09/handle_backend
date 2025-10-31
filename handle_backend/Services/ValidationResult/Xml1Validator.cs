/*using System.Xml;

namespace handle_backend.Services.ValidationResult
{
    public class Xml1Validator : IValidator
    {
        private readonly ValidationContext _context;

        public Xml1Validator(ValidationContext context)
        {
            _context = context;
        }

        public ValidationResult Validate(XmlDocument xmlDoc, string ngay_vv, string ngay_rv, string ngaythanhtoan)
        {
            var result = new ValidationResult();
            XmlNodeList nodeYL = xmlDoc.SelectNodes("//TONG_HOP");

            if (nodeYL != null && nodeYL.Count > 0)
            {
                foreach (XmlNode chiTietNode in nodeYL)
                {
                    XmlNode mabn = chiTietNode.SelectSingleNode("MA_BN");
                    XmlNode hotenbn = chiTietNode.SelectSingleNode("HO_TEN");
                    XmlNode socccd = chiTietNode.SelectSingleNode("SO_CCCD");
                    XmlNode lydovv = chiTietNode.SelectSingleNode("LY_DO_VV");
                    XmlNode ppdieutri = chiTietNode.SelectSingleNode("PP_DIEU_TRI");
                    XmlNode mabenhchinh = chiTietNode.SelectSingleNode("MA_BENH_CHINH");
                    XmlNode chandoanvv = chiTietNode.SelectSingleNode("CHAN_DOAN_VAO");
                    XmlNode chandoanrv = chiTietNode.SelectSingleNode("CHAN_DOAN_RV");
                    XmlNode ma_nghe_nghiep = chiTietNode.SelectSingleNode("MA_NGHE_NGHIEP");
                    XmlNode maquoctich = chiTietNode.SelectSingleNode("MA_QUOCTICH");
                    XmlNode lydovnt = chiTietNode.SelectSingleNode("LY_DO_VNT");
                    XmlNode maloaikcb = chiTietNode.SelectSingleNode("MA_LOAI_KCB");
                    XmlNode ngayvao = chiTietNode.SelectSingleNode("NGAY_VAO");
                    XmlNode ngayra = chiTietNode.SelectSingleNode("NGAY_RA");
                    XmlNode ngayttoan = chiTietNode.SelectSingleNode("NGAY_TTOAN");
                    XmlNode giatritu = chiTietNode.SelectSingleNode("GT_THE_TU");
                    XmlNode giatriden = chiTietNode.SelectSingleNode("GT_THE_DEN");
                    XmlNode namqt = chiTietNode.SelectSingleNode("NAM_QT");
                    XmlNode thangqt = chiTietNode.SelectSingleNode("THANG_QT");
                    XmlNode ma_ttdv = chiTietNode.SelectSingleNode("MA_TTDV");

                    result.PatientName = hotenbn?.InnerText.Trim() ?? "";
                    result.PatientId = mabn?.InnerText.Trim() ?? "";
                    ngay_vv = ngayvao?.InnerText.Trim() ?? "";
                    ngay_rv = ngayra?.InnerText.Trim() ?? "";
                    ngaythanhtoan = ngayttoan?.InnerText.Trim() ?? "";

                    _context.ValidateRequiredField(lydovv, "LY_DO_VV", result.Errors);
                    _context.ValidateRequiredField(ppdieutri, "PP_DIEU_TRI", result.Errors);
                    _context.ValidateRequiredField(mabenhchinh, "MA_BENH_CHINH", result.Errors);
                    _context.ValidateRequiredField(chandoanvv, "CHAN_DOAN_VAO", result.Errors);
                    _context.ValidateRequiredField(chandoanrv, "CHAN_DOAN_RV", result.Errors);
                    _context.ValidateRequiredField(ma_nghe_nghiep, "MA_NGHE_NGHIEP", result.Errors);
                    _context.ValidateRequiredField(namqt, "NAM_QT", result.Errors);
                    _context.ValidateRequiredField(thangqt, "THANG_QT", result.Errors);

                    if (maquoctich != null && maquoctich.InnerText != "000")
                        result.Errors.Add("Sai quốc tịch");

                    if (lydovnt != null && string.IsNullOrWhiteSpace(lydovnt.InnerText) && maloaikcb?.InnerText == "3")
                        result.Errors.Add("Trường LY_DO_VNT không được để trống");

                    _context.ValidateDateOrder(ngayvao, ngayra, "NGAY_VAO", "NGAY_RA", result.Errors);
                    _context.ValidateDateOrder(ngayvao, ngayttoan, "NGAY_VAO", "NGAY_TTOAN", result.Errors);
                    _context.ValidateDateOrder(ngayra, ngayttoan, "NGAY_RA", "NGAY_TTOAN", result.Errors);
                    _context.ValidateDateOrder(giatritu, giatriden, "GT_THE_TU", "GT_THE_DEN", result.Errors, appendTime: true);

                    _context.ValidateFormat(socccd, @"^\d{12}$", "SO_CCCD", result.Errors, checkIfNotEmpty: true);
                    _context.ValidateValue(ma_ttdv, _context._mattdv, "MA_TTDV", result.Errors);
                }
            }

            return result;
        }
    }
}
*/