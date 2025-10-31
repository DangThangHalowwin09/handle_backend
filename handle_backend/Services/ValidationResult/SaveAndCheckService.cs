/*using System.Xml;

namespace handle_backend.Services.ValidationResult
{
    public class SaveAndCheckService
    {
        private readonly ValidationContext _context;
        private readonly Dictionary<string, IValidator> _validators;

        public SaveAndCheckService(string mattdv = "2997032342")
        {
            _context = new ValidationContext(mattdv);
            _validators = new Dictionary<string, IValidator>
        {
            { "XML1", new Xml1Validator(_context) },
            { "XML2", new Xml2Validator(_context) },
            // Add validators for XML3, XML4, XML5, XML7, XML8, XML11, XML14
        };
        }

        public ValidationResult SaveAndCheck(string strNode, string decodedString)
        {
            var result = new ValidationResult();
            string ngay_vv = "", ngay_rv = "", ngaythanhtoan = "";

            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(decodedString);

                // Extract patient information
                XmlNodeList nodeYL2 = xmlDoc.SelectNodes("//TONG_HOP");
                if (nodeYL2 != null && nodeYL2.Count > 0)
                {
                    foreach (XmlNode chiTietNode in nodeYL2)
                    {
                        XmlNode mabn = chiTietNode.SelectSingleNode("MA_BN");
                        XmlNode hotenbn = chiTietNode.SelectSingleNode("HO_TEN");
                        result.PatientName = hotenbn?.InnerText.Trim() ?? "";
                        result.PatientId = mabn?.InnerText.Trim() ?? "";
                    }
                }

                if (_validators.TryGetValue(strNode, out var validator))
                {
                    result = validator.Validate(xmlDoc, ngay_vv, ngay_rv, ngaythanhtoan);
                }
                else
                {
                    result.Errors.Add($"Không hỗ trợ loại XML: {strNode}");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Lỗi xử lý XML {strNode}: {ex.Message}");
            }

            return result;
        }
    }
}
*/