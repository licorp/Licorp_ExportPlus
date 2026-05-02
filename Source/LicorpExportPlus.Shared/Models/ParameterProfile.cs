using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace LicorpExportPlus.Models
{
    /// <summary>
    /// Lưu trữ profile parameters người dùng đã chọn
    /// Được lưu vào file JSON để nhớ cho lần sau
    /// </summary>
    public class ParameterProfile
    {
        private static readonly string ProfilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LicorpExportPlus",
            "ExportPlus",
            "parameter_profile.json"
        );

        /// <summary>
        /// Danh sách parameters user đã sử dụng trong custom filename
        /// Được sắp xếp theo thứ tự ưu tiên (dùng nhiều nhất → ít nhất)
        /// </summary>
        public List<string> FrequentParameters { get; set; } = new List<string>();

        /// <summary>
        /// Thời gian profile được cập nhật lần cuối
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        /// <summary>
        /// Load profile từ file JSON
        /// </summary>
        public static ParameterProfile Load()
        {
            try
            {
                if (!File.Exists(ProfilePath))
                {
                    return new ParameterProfile();
                }

                var json = File.ReadAllText(ProfilePath);
                return JsonConvert.DeserializeObject<ParameterProfile>(json) ?? new ParameterProfile();
            }
            catch (Exception ex)
            {
                Licorp.Diagnostics.LicorpTrace.Dbg($"[Export +] ⚠️ Failed to load parameter profile: {ex.Message}");
                return new ParameterProfile();
            }
        }

        /// <summary>
        /// Lưu profile ra file JSON
        /// </summary>
        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(ProfilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                LastUpdated = DateTime.Now;
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(ProfilePath, json);

                Licorp.Diagnostics.LicorpTrace.Dbg($"[Export +] ✅ Parameter profile saved: {FrequentParameters.Count} parameters");
            }
            catch (Exception ex)
            {
                Licorp.Diagnostics.LicorpTrace.Dbg($"[Export +] ⚠️ Failed to save parameter profile: {ex.Message}");
            }
        }

        /// <summary>
        /// Thêm parameter vào profile (tăng độ ưu tiên)
        /// </summary>
        public void AddParameter(string parameterName)
        {
            if (string.IsNullOrEmpty(parameterName)) return;

            // Nếu đã có → đưa lên đầu (tăng priority)
            if (FrequentParameters.Contains(parameterName))
            {
                FrequentParameters.Remove(parameterName);
            }

            // Thêm vào đầu danh sách
            FrequentParameters.Insert(0, parameterName);

            // Giới hạn 20 parameters gần nhất
            if (FrequentParameters.Count > 20)
            {
                FrequentParameters = FrequentParameters.Take(20).ToList();
            }
        }

        /// <summary>
        /// Thêm nhiều parameters cùng lúc
        /// </summary>
        public void AddParameters(IEnumerable<string> parameterNames)
        {
            foreach (var param in parameterNames)
            {
                AddParameter(param);
            }
        }

        /// <summary>
        /// Get top N parameters thường dùng nhất
        /// </summary>
        public List<string> GetTopParameters(int count = 10)
        {
            return FrequentParameters.Take(count).ToList();
        }

        /// <summary>
        /// Clear profile
        /// </summary>
        public void Clear()
        {
            FrequentParameters.Clear();
            Save();
        }
    }
}
