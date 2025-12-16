using System;

namespace AttendanceSystem
{
    public class SessionInfo
    {
        public string Department { get; set; } = string.Empty;
        public double LoginDurationMinutes { get; set; } = 15;
        public double LectureDurationMinutes { get; set; } = 60;
        public DateTime StartTime { get; set; } = DateTime.Now;
        public int Grade { get; set; } = 1;
        public string SessionId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string GradeString
        {
            get
            {
                return Grade switch
                {
                    1 => "الفرقة الأولى",
                    2 => "الفرقة الثانية",
                    3 => "الفرقة الثالثة",
                    4 => "الفرقة الرابعة",
                    _ => "غير محدد"
                };
            }
        }
    }
}
