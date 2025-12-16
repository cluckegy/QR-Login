using System;

namespace AttendanceSystem
{
    public class Student
    {
        public int Id { get; set; }
        public int Index => Id; // For DataGrid binding
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
        public DateTime RegisterTime { get; set; } = DateTime.Now;
        public DateTime? LogoutTime { get; set; }
        
        // Track if student left the page (possibly opened camera)
        public int LeftPageCount { get; set; } = 0;
        public string Status => LeftPageCount > 0 ? $"⚠️ Left {LeftPageCount}x" : "✓ Active";
    }
}
