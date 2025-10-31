/*using System.Text.RegularExpressions;
using System.Xml;

namespace handle_backend.Services.ValidationResult
{
    public class ValidationResult
    {
        public string PatientName { get; set; } = "";
        public string PatientId { get; set; } = "";
        public List<string> Errors { get; } = new List<string>();
    }

    public interface IValidator
    {
        ValidationResult Validate(XmlDocument xmlDoc, string ngay_vv, string ngay_rv, string ngaythanhtoan);
    }

    public class ValidationContext
    {
        private readonly string _mattdv;

        public ValidationContext(string mattdv)
        {
            _mattdv = mattdv;
        }

        public void ValidateRequiredField(XmlNode node, string fieldName, List<string> errors)
        {
            if (node != null && string.IsNullOrWhiteSpace(node.InnerText))
                errors.Add($"{fieldName} không được để trống");
        }

        public void ValidateFormat(XmlNode node, string pattern, string fieldName, List<string> errors, bool checkIfNotEmpty = false)
        {
            if (node != null && (!checkIfNotEmpty || !string.IsNullOrEmpty(node.InnerText)))
                if (!checkformat(node.InnerText.Trim(), pattern))
                    errors.Add($"{fieldName} không đúng định dạng");
        }

        public void ValidateDateOrder(XmlNode startNode, XmlNode endNode, string startFieldName, string endFieldName, List<string> errors, bool appendTime = false)
        {
            if (startNode != null && endNode != null && !string.IsNullOrWhiteSpace(startNode.InnerText) && !string.IsNullOrWhiteSpace(endNode.InnerText))
                if (!SosanhTime2(appendTime ? startNode.InnerText + "0000" : startNode.InnerText, appendTime ? endNode.InnerText + "0000" : endNode.InnerText))
                    errors.Add($"{endFieldName} không được nhỏ hơn {startFieldName}");
        }

        public void ValidateValue(XmlNode node, string expectedValue, string fieldName, List<string> errors)
        {
            if (node != null && node.InnerText != expectedValue)
                errors.Add($"Thông tin {fieldName} sai");
        }

        public bool checkformat(string input, string pattern) => Regex.IsMatch(input, pattern); // Placeholder
        public bool SosanhTime2(string start, string end) => true; // Placeholder
        public bool checkChar(string input) => false; // Placeholder
        public string CleanInput(string input) => input; // Placeholder
    }
}
*/