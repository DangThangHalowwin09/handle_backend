namespace handle_backend.Services.XML.VTYT
{
    public static class Xuly_vtyt
    {
        private static readonly HashSet<string> _danhMucCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static Xuly_vtyt()
        {
            // Khởi tạo HashSet từ mảng khi lớp được load lần đầu
            foreach (var code in handle_backend.Services.XML.VTYT.Danhmuc_vtyt.DanhMucVatTuYTeArray)
            {
                if (!string.IsNullOrWhiteSpace(code))
                {
                    // Thêm vào HashSet, loại bỏ các mã trùng lặp (nếu có)
                    _danhMucCodes.Add(code.Trim());
                }
            }
        }
        public static bool KiemTraTonTai(string maMay)
        {
            return _danhMucCodes.Contains(maMay.Trim());
        }
    }
}
