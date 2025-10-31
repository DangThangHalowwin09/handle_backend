using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using System;
using System.Threading.Tasks;

namespace handle_backend.Services.Firebase
{
    public class FirebaseService
    {
        private readonly FirestoreDb _firestore;

        public FirebaseService()
        {
            // ⚠️ Đường dẫn thật tới file JSON bạn tải về
            string path = @"D:\Firebase\test-937d3-firebase-adminsdk-fbsvc-c29971c78f.json";
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", path);

            // ⚠️ Thay bằng Project ID thật trong Firebase console
            _firestore = FirestoreDb.Create("test-937d3");
            //Console.WriteLine("✅ Firebase Firestore initialized!");
        }

        public async Task AddError_BHYT(string patientName, string patientCode, string errorMessage)
        {
            var docRef = _firestore.Collection("errors_BHYT").Document(patientCode);

            // Lấy dữ liệu hiện tại (nếu có)
            var snapshot = await docRef.GetSnapshotAsync();
            string existingError = snapshot.Exists && snapshot.ContainsField("error")
                ? snapshot.GetValue<string>("error")
                : "";

            // Gộp thêm lỗi mới (xuống dòng)
            string updatedError = string.IsNullOrEmpty(existingError)
                ? errorMessage
                : $"{existingError}\n{errorMessage}";

            var data = new
            {
                name = patientName,
                code = patientCode,
                error = updatedError,
                updatedAt = DateTime.UtcNow
            };

            await docRef.SetAsync(data);
            Console.WriteLine($"✅ Cập nhật lỗi cho bệnh nhân {patientName} ({patientCode}) thành công!");
        }
        public async Task AddError_HandlingFile(string patientName, string patientCode, string errorMessage)
        {
            var docRef = _firestore.Collection("errors_BHYT").Document(patientCode);

            // Lấy dữ liệu hiện tại (nếu có)
            var snapshot = await docRef.GetSnapshotAsync();
            string existingError = snapshot.Exists && snapshot.ContainsField("error_handling_file")
                ? snapshot.GetValue<string>("error_handling_file")
                : "";

            // Gộp thêm lỗi mới (xuống dòng)
            string updatedError = string.IsNullOrEmpty(existingError)
                ? errorMessage
                : $"{existingError}\n{errorMessage}";

            var data = new
            {
                name = patientName,
                code = patientCode,
                error = updatedError,
                updatedAt = DateTime.UtcNow
            };

            await docRef.SetAsync(data);
            //Console.WriteLine($"✅ Cập nhật lỗi cho bệnh nhân {patientName} ({patientCode}) thành công!");
        }
    }
}